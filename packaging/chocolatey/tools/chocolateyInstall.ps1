# NOTE TO MAINTAINER:
# Replace <SHA256_OF_Setup-x64.msi> with the real SHA-256 hash before packing.
# Compute with: (Get-FileHash "Setup-x64.msi" -Algorithm SHA256).Hash

$ErrorActionPreference = 'Stop'

$packageName   = 'fluenttaskscheduler'
$toolsDir      = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$version       = '1.9.0'

# Detect architecture
$isArm64 = ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') -or ($env:PROCESSOR_ARCHITEW6432 -eq 'ARM64')

# Use correct 'V' prefix for GitHub release tag
$pkgUrl = "https://github.com/TRGamer-tech/FluentTaskScheduler/releases/download/V$version/Setup-x64.msi"
$pkgHash = 'C21DBD04F6C50517370DE0DC6F604A921A932DE9CD75DA7FEC717E4ADF4F7A6C'

if ($isArm64) {
    $pkgUrl = "https://github.com/TRGamer-tech/FluentTaskScheduler/releases/download/V$version/Setup-arm64.msi"
    $pkgHash = '4CB3BD07E20FC8990A31F7D89BCEBBD8EE2ED8B77BCEE52C6345DFD3FAFF28D6'
}

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
