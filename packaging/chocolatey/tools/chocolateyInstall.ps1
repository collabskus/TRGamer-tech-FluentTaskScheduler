$ErrorActionPreference = 'Stop'

$packageName   = 'fluenttaskscheduler'
$toolsDir      = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$version       = '1.9.0'

# Detect architecture
$isArm64 = ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') -or ($env:PROCESSOR_ARCHITEW6432 -eq 'ARM64')

# Use correct 'V' prefix for GitHub release tag
$pkgUrl = "https://github.com/TRGamer-tech/FluentTaskScheduler/releases/download/V$version/Setup-x64.msi"
$pkgHash = '833360EBB51466EE2C207FBB0BBFC7327FD1E31D125E64249F71BEF9DE11A944'

if ($isArm64) {
    $pkgUrl = "https://github.com/TRGamer-tech/FluentTaskScheduler/releases/download/V$version/Setup-arm64.msi"
    $pkgHash = '99659DF3FB0C06113BA966A561BE4E7AA711FF2C06EEF9C976B035849953450D'
}

# Chocolatey's helpers only expose url/checksum (32-bit) and url64bit/checksum64 (64-bit) — there is
# no dedicated ARM64 slot. url64bit is reused here for both x64 and ARM64 after the manual
# architecture detection above; this is the standard workaround other ARM64 Chocolatey packages use.
$packageArgs = @{
  packageName    = $packageName
  fileType       = 'msi'
  url64bit       = $pkgUrl
  checksum64     = $pkgHash
  checksumType64 = 'sha256'
  silentArgs     = '/qn /norestart'
  validExitCodes = @(0, 3010, 1641)
}

Install-ChocolateyPackage @packageArgs
