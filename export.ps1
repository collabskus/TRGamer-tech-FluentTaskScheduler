#!/usr/bin/env pwsh
# =============================================================================
# export.ps1 — Repository context dump for LLM consumption (PowerShell 7)
#
# Location: <repository root>/export.ps1
#
# Usage:  pwsh export.ps1
#         Measure-Command { pwsh export.ps1 }
# =============================================================================

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# 0. Resolve the script's own location — immune to caller's directory
# ---------------------------------------------------------------------------
$ScriptPath = $MyInvocation.MyCommand.Path
if (-not $ScriptPath) { $ScriptPath = $PSCommandPath }
$ScriptDirectory = Split-Path -Parent $ScriptPath
$ScriptName = Split-Path -Leaf $ScriptPath

# ---------------------------------------------------------------------------
# 1. Validate: must be inside a working Git repository
# ---------------------------------------------------------------------------
$isRepo = git -C "$ScriptDirectory" rev-parse --is-inside-work-tree 2>$null
if ($LASTEXITCODE -ne 0 -or $isRepo.Trim() -ne 'true') { exit 0 }

$status = git -C "$ScriptDirectory" status --porcelain 2>$null
if ($LASTEXITCODE -ne 0) { exit 0 }

$RepoRoot = (git -C "$ScriptDirectory" rev-parse --show-toplevel 2>$null).Trim().Replace('\', '/')
if (-not $RepoRoot) { exit 0 }

# ---------------------------------------------------------------------------
# 2. Constants & derived paths
# ---------------------------------------------------------------------------
$ExcludedDirectory = "docs/llm"
$ExcludedFiles = @($ScriptName)
$ExcludedFilesDisplay = $ExcludedFiles -join ", "

$OutputDirectory = "$RepoRoot/$ExcludedDirectory"
$OutputFile = "$OutputDirectory/dump.txt"

$Timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssK")
$GitBranch = (git -C "$RepoRoot" rev-parse --abbrev-ref HEAD 2>$null) ?? "unknown"
$GitCommit = (git -C "$RepoRoot" rev-parse HEAD 2>$null) ?? "unknown"
$GitCommitShort = (git -C "$RepoRoot" rev-parse --short HEAD 2>$null) ?? "unknown"
$GitCommitMessage = (git -C "$RepoRoot" log -1 --pretty=format:'%s' 2>$null) ?? "unknown"
$GitCommitDate = (git -C "$RepoRoot" log -1 --pretty=format:'%ci' 2>$null) ?? "unknown"
$GitRemote = (git -C "$RepoRoot" remote get-url origin 2>$null) ?? "none"
$GitStatusSummary = (git -C "$RepoRoot" status --short 2>$null | Select-Object -First 20) -join "`n"

$Hostname = [Environment]::MachineName
$Username = [Environment]::UserName
$OperatingSystem = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription

# ---------------------------------------------------------------------------
# 3. Collect tracked files (staged/committed only)
# ---------------------------------------------------------------------------
$rawFilesBytes = git -C "$RepoRoot" ls-files --cached -z 2>$null
$rawTracked = if ($rawFilesBytes) {
    ($rawFilesBytes -split "`0") | Where-Object { $_ } | Sort-Object -Unique
} else { @() }

$IncludedFiles = [System.Collections.Generic.List[string]]::new()
foreach ($candidate in $rawTracked) {
    $candidate = $candidate.Replace('\', '/')
    if ([string]::IsNullOrWhiteSpace($candidate)) { continue }

    # Skip excluded directory tree
    if ($candidate.StartsWith("$ExcludedDirectory/")) { continue }

    # Skip individually excluded files
    $candidateExcluded = $false
    foreach ($entry in $ExcludedFiles) {
        if ($entry.Contains('/')) {
            if ($candidate -eq $entry) { $candidateExcluded = $true; break }
        } else {
            if ($candidate -eq $entry -or $candidate.EndsWith("/$entry")) { $candidateExcluded = $true; break }
        }
    }
    if ($candidateExcluded) { continue }

    $IncludedFiles.Add($candidate)
}

$FileCount = $IncludedFiles.Count
$FileCountNoun = if ($FileCount -eq 1) { "file" } else { "files" }

# ---------------------------------------------------------------------------
# 4. Helpers: Formatting, Hashes & Binary Detection
# ---------------------------------------------------------------------------
function Format-HumanSize ([long]$Bytes) {
    if ($Bytes -lt 1024) { return "$Bytes B" }
    $units = @("KiB", "MiB", "GiB", "TiB", "PiB")
    $val = [double]$Bytes
    $unitIndex = -1
    while ($val -ge 1024 -and $unitIndex -lt ($units.Count - 1)) {
        $val /= 1024
        $unitIndex++
    }
    return "{0:N1} {1}" -f $val, $units[$unitIndex]
}

function Get-FileSha256 ([string]$Path) {
    if (-not (Test-Path $Path -PathType Leaf)) { return "unavailable" }
    try {
        return (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLower()
    } catch {
        return "unavailable"
    }
}

function Get-FileMimeType ([string]$Path) {
    $ext = [System.IO.Path]::GetExtension($Path).ToLower()
    $mimeTypes = @{
        ".txt"="text/plain"; ".md"="text/markdown"; ".json"="application/json";
        ".js"="application/javascript"; ".ts"="application/typescript";
        ".py"="text/x-python"; ".sh"="application/x-shellscript";
        ".ps1"="text/x-powershell"; ".html"="text/html"; ".css"="text/css";
        ".xml"="application/xml"; ".yaml"="application/yaml"; ".yml"="application/yaml";
        ".png"="image/png"; ".jpg"="image/jpeg"; ".svg"="image/svg+xml";
        ".pdf"="application/pdf"; ".zip"="application/zip"; ".gz"="application/gzip"
    }
    if ($mimeTypes.ContainsKey($ext)) { return $mimeTypes[$ext] }
    return "application/octet-stream"
}

function Test-IsBinaryFile ([string]$Path) {
    if (-not (Test-Path $Path -PathType Leaf)) { return $false }
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        $buffer = New-Object byte[] 8192
        $bytesRead = $stream.Read($buffer, 0, $buffer.Length)
        $stream.Close()
        $stream.Dispose()
        for ($i = 0; $i -lt $bytesRead; $i++) {
            if ($buffer[$i] -eq 0) { return $true }
        }
    } catch {
        return $false
    }
    return $false
}

# ---------------------------------------------------------------------------
# 5. Helper: Tree Renderer
# ---------------------------------------------------------------------------
function Format-FileTree {
    if ($FileCount -eq 0) { return ".\n(no files included)" }

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine(".")

    script:RenderTreeLevel "" "" $sb
    return $sb.ToString().TrimEnd()
}

function RenderTreeLevel ([string]$ParentPath, [string]$Prefix, [System.Text.StringBuilder]$sb) {
    $childNames = [System.Collections.Generic.List[string]]::new()
    $prevChild = ""

    foreach ($entry in $IncludedFiles) {
        $remainder = $entry
        if ($ParentPath) {
            if (-not $entry.StartsWith("$ParentPath/")) { continue }
            $remainder = $entry.Substring($ParentPath.Length + 1)
        }
        $childName = $remainder.Split('/')[0]
        if ($childName -ne $prevChild) {
            $childNames.Add($childName)
            $prevChild = $childName
        }
    }

    $childCount = $childNames.Count
    for ($i = 0; $i -lt $childCount; $i++) {
        $childName = $childNames[$i]
        $isLast = ($i -eq $childCount - 1)
        $connector = if ($isLast) { "└── " } else { "├── " }
        $descendantPrefix = if ($isLast) { "$Prefix    " } else { "$Prefix│   " }

        $childPath = if ($ParentPath) { "$ParentPath/$childName" } else { $childName }
        $isDirectory = $IncludedFiles | Where-Object { $_.StartsWith("$childPath/") } | Select-Object -First 1

        if ($isDirectory) {
            [void]$sb.AppendLine("${Prefix}${connector}${childName}/")
            RenderTreeLevel $childPath $descendantPrefix $sb
        } else {
            [void]$sb.AppendLine("${Prefix}${connector}${childName}")
        }
    }
}

# ---------------------------------------------------------------------------
# 6. Metadata and Content Printers
# ---------------------------------------------------------------------------
function Get-FileMetadataText ([string]$AbsPath, [string]$RelPath, [long]$Size, [string]$Sha256) {
    $item = Get-Item $AbsPath -ErrorAction SilentlyContinue
    $modTime = if ($item) { $item.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss zzz") } else { "unavailable" }
    $permissions = if ($item) { $item.Attributes.ToString() } else { "unavailable" }
    $mimeType = Get-FileMimeType $AbsPath
    $lastCommit = (git -C "$RepoRoot" log -1 --pretty=format:'%h %ai %s' -- "$RelPath" 2>$null)
    if ([string]::IsNullOrWhiteSpace($lastCommit)) { $lastCommit = "(not yet committed)" }

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("`n--- METADATA ---")
    [void]$sb.AppendLine(("  {0,-22} {1}" -f "File name:", [System.IO.Path]::GetFileName($RelPath)))
    [void]$sb.AppendLine(("  {0,-22} {1}" -f "Relative path:", $RelPath))
    [void]$sb.AppendLine(("  {0,-22} {1}" -f "Absolute path:", $AbsPath))
    [void]$sb.AppendLine(("  {0,-22} {1}" -f "Size:", "$(Format-HumanSize $Size) ($Size bytes)"))
    [void]$sb.AppendLine(("  {0,-22} {1}" -f "Last modified:", $modTime))
    [void]$sb.AppendLine(("  {0,-22} {1}" -f "Permissions:", $permissions))
    [void]$sb.AppendLine(("  {0,-22} {1}" -f "MIME type:", $mimeType))
    [void]$sb.AppendLine(("  {0,-22} {1}" -f "SHA-256:", $Sha256))
    [void]$sb.AppendLine(("  {0,-22} {1}" -f "Last git commit:", $lastCommit))
    [void]$sb.AppendLine("`n--- CONTENT ---")
    return $sb.ToString()
}

function Get-FileContentText ([string]$AbsPath, [long]$Size, [string]$Sha256) {
    if (Test-IsBinaryFile $AbsPath) {
        return "[Binary file — content omitted. Size: $(Format-HumanSize $Size), SHA-256: $Sha256]`n"
    }
    $content = [System.IO.File]::ReadAllText($AbsPath)
    if (-not $content.EndsWith("`n")) {
        $content += "`n"
    }
    return $content
}

# ---------------------------------------------------------------------------
# 7. Assemble the complete dump
# ---------------------------------------------------------------------------
function Generate-Dump {
    $sb = [System.Text.StringBuilder]::new()

    [void]$sb.AppendLine(@"
################################################################################
#                                                                              #
#   REPOSITORY CONTEXT DUMP                                                    #
#   Generated for LLM consumption — do not edit manually                       #
#                                                                              #
################################################################################

DUMP METADATA
═════════════════════════════════════════════════════════════════════════════════
  Generated at   : ${Timestamp}
  Generator      : ${ScriptName}
  Host           : ${Hostname}
  User           : ${Username}
  OS             : ${OperatingSystem}

REPOSITORY METADATA
═════════════════════════════════════════════════════════════════════════════════
  Repository root: ${RepoRoot}
  Branch         : ${GitBranch}
  Commit (full)  : ${GitCommit}
  Commit (short) : ${GitCommitShort}
  Commit date    : ${GitCommitDate}
  Commit message : ${GitCommitMessage}
  Remote origin  : ${GitRemote}
  Files included : ${FileCount}
  Excluded path  : ${ExcludedDirectory}/
  Excluded files : ${ExcludedFilesDisplay}

GIT WORKING TREE STATUS (first 20 lines)
═════════════════════════════════════════════════════════════════════════════════
"@)

    if ($GitStatusSummary) {
        [void]$sb.AppendLine($GitStatusSummary)
    } else {
        [void]$sb.AppendLine("  (clean — no uncommitted changes)")
    }

    # Self-documentation: this script, exactly once
    [void]$sb.AppendLine(@"

################################################################################
# FILE: ${ScriptName}  [THIS SCRIPT — included for full context]
################################################################################
"@)

    $scriptAbsPath = (Resolve-Path $ScriptPath).Path.Replace('\', '/')
    $scriptRelPath = $scriptAbsPath.Replace("$RepoRoot/", "")
    $scriptSize = (Get-Item $scriptAbsPath).Length
    $scriptSha256 = Get-FileSha256 $scriptAbsPath

    [void]$sb.Append((Get-FileMetadataText $scriptAbsPath $scriptRelPath $scriptSize $scriptSha256))
    [void]$sb.Append((Get-FileContentText $scriptAbsPath $scriptSize $scriptSha256))

    # File Tree
    [void]$sb.AppendLine(@"

################################################################################
# FILE TREE  (${FileCount} included ${FileCountNoun})
################################################################################
"@)
    [void]$sb.AppendLine((Format-FileTree))
    [void]$sb.AppendLine("")

    # Per-file content dump
    [long]$totalBytes = 0
    foreach ($relPath in $IncludedFiles) {
        $absPath = "$RepoRoot/$relPath"
        if (-not (Test-Path $absPath -PathType Leaf)) { continue }

        $item = Get-Item $absPath -ErrorAction SilentlyContinue
        $fileSize = if ($item) { $item.Length } else { 0 }
        $totalBytes += $fileSize
        $sha256Value = Get-FileSha256 $absPath

        [void]$sb.AppendLine("`n################################################################################")
        [void]$sb.AppendLine("# FILE: $relPath")
        [void]$sb.AppendLine("################################################################################")
        [void]$sb.Append((Get-FileMetadataText $absPath $relPath $fileSize $sha256Value))
        [void]$sb.Append((Get-FileContentText $absPath $fileSize $sha256Value))
    }

    # Summary
    $completedAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssK")
    [void]$sb.AppendLine(@"

################################################################################
# DUMP SUMMARY
################################################################################
  Files dumped   : ${FileCount}
  Total size     : $(Format-HumanSize $totalBytes) (${totalBytes} bytes)
  Output file    : ${OutputFile}
  Completed at   : ${completedAt}
################################################################################
# END OF DUMP
################################################################################
"@)

    return $sb.ToString()
}

# ---------------------------------------------------------------------------
# 8. Write atomically to file, then output to console
# ---------------------------------------------------------------------------
if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

$TempFile = [System.IO.Path]::GetTempFileName()

try {
    $dumpContent = Generate-Dump
    [System.IO.File]::WriteAllText($TempFile, $dumpContent, [System.Text.Encoding]::UTF8)
    
    # Atomic replace
    Move-Item -Path $TempFile -Destination $OutputFile -Force
    
    # Echo exact written file contents to console
    Get-Content $OutputFile -Raw
} finally {
    if (Test-Path $TempFile) {
        Remove-Item $TempFile -Force -ErrorAction SilentlyContinue
    }
}