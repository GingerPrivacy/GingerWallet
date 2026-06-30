# Script downloads Tor Expert Bundles, verifies the signed checksum file,
# extracts the required Tor runtime files, and replaces the bundled Tor binaries.
#
# Examples:
# 1] .\UpgradeTorBinaries.ps1 -version "15.0.17"
# 2] .\UpgradeTorBinaries.ps1 -version "15.0.17" -skipDownloading
#
# Requirements:
# 1) PowerShell 7+
# 2) tar on PATH
# 3) gpg on PATH for Tor Project checksum signature verification

[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)] [string] $version,
  [Parameter(Mandatory=$false)] [Switch] $skipDownloading,
  [Parameter(Mandatory=$false)] [Alias("skipExtractingBrowserArchives", "skipExtractingTorBinaries")] [Switch] $skipExtractingArchives,
  [Parameter(Mandatory=$false)] [Switch] $skipReplacingTorBinaries,
  [Parameter(Mandatory=$false)] [Switch] $skipReplacingGeoIpFiles)

Set-StrictMode -Version 3
$ErrorActionPreference = "Stop"

$distUri = "https://dist.torproject.org/torbrowser/${version}"
$checksumFileName = "sha256sums-signed-build.txt"
$checksumSignatureFileName = "${checksumFileName}.asc"
$signingKeyFingerprint = "EF6E286DDA85EA2A4BA7DE684E2C6E8793298290"
$signingKeyFileName = "tor-browser-developers.asc"
$signingKeyUri = "https://keys.openpgp.org/vks/v1/by-fingerprint/${signingKeyFingerprint}"

$platforms = @(
  @{
    Name = "win64"
    Package = "tor-expert-bundle-windows-x86_64-${version}.tar.gz"
    RuntimeFiles = @("tor.exe")
  },
  @{
    Name = "lin64"
    Package = "tor-expert-bundle-linux-x86_64-${version}.tar.gz"
    RuntimeFiles = @("libcrypto.so.3", "libevent-2.1.so.7", "libssl.so.3", "tor")
  },
  @{
    Name = "osx64"
    Package = "tor-expert-bundle-macos-x86_64-${version}.tar.gz"
    RuntimeFiles = @("libevent-2.1.7.dylib", "tor")
  },
  @{
    Name = "osx-arm64"
    Package = "tor-expert-bundle-macos-aarch64-${version}.tar.gz"
    RuntimeFiles = @("libevent-2.1.7.dylib", "tor")
  }
)

function Assert-CommandExists {
  param([Parameter(Mandatory=$true)] [string] $command)

  if ($null -eq (Get-Command $command -ErrorAction SilentlyContinue)) {
    throw "Required command '$command' was not found on PATH."
  }
}

function Invoke-CheckedCommand {
  param(
    [Parameter(Mandatory=$true)] [string] $command,
    [Parameter(Mandatory=$true)] [string[]] $arguments)

  Write-Output "# Running: $command $($arguments -join ' ')"
  & $command @arguments
  if ($LASTEXITCODE -ne 0) {
    throw "Command '$command' failed with exit code $LASTEXITCODE."
  }
}

function Invoke-DownloadFile {
  param([Parameter(Mandatory=$true)] [string] $fileName)

  $uri = "${distUri}/${fileName}"
  Invoke-DownloadUri $uri $fileName
}

function Invoke-DownloadUri {
  param(
    [Parameter(Mandatory=$true)] [string] $uri,
    [Parameter(Mandatory=$true)] [string] $fileName)

  Write-Output "# Downloading ${uri} ..."
  Invoke-WebRequest -Uri $uri -OutFile $fileName
}

function Assert-SigningKeyFingerprint {
  param([Parameter(Mandatory=$true)] [string] $keyFilePath)

  $keyInfo = & gpg --batch --with-colons --show-keys $keyFilePath
  if ($LASTEXITCODE -ne 0) {
    throw "Failed to read Tor Browser Developers signing key."
  }

  $fingerprints = $keyInfo |
    Where-Object { $_.StartsWith("fpr:") } |
    ForEach-Object { $_.Split(":")[9] }

  if ($fingerprints -notcontains $signingKeyFingerprint) {
    throw "Downloaded signing key does not contain expected fingerprint '${signingKeyFingerprint}'."
  }
}

function Assert-ChecksumSignature {
  Assert-CommandExists "gpg"

  if (!(Test-Path -LiteralPath $signingKeyFileName)) {
    Invoke-DownloadUri $signingKeyUri $signingKeyFileName
  }

  Assert-SigningKeyFingerprint $signingKeyFileName

  $gnupgHome = Join-Path (Get-Location) "gnupg"
  Remove-Item -LiteralPath $gnupgHome -Force -Recurse -ErrorAction SilentlyContinue
  New-Item -Path $gnupgHome -ItemType Directory -Force | Out-Null

  Invoke-CheckedCommand "gpg" @("--batch", "--homedir", $gnupgHome, "--import", $signingKeyFileName)
  Invoke-CheckedCommand "gpg" @("--batch", "--homedir", $gnupgHome, "--verify", $checksumSignatureFileName, $checksumFileName)
}

function Get-ExpectedHashes {
  $hashes = @{}

  foreach ($line in Get-Content -LiteralPath $checksumFileName) {
    if ($line -match "^(?<hash>[a-fA-F0-9]{64})\s+(?<file>\S+)\s*$") {
      $hashes[$Matches["file"]] = $Matches["hash"].ToLowerInvariant()
    }
  }

  return $hashes
}

function Assert-FileHash {
  param(
    [Parameter(Mandatory=$true)] [hashtable] $expectedHashes,
    [Parameter(Mandatory=$true)] [string] $fileName)

  if (!$expectedHashes.ContainsKey($fileName)) {
    throw "Checksum file does not contain '${fileName}'."
  }

  $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $fileName).Hash.ToLowerInvariant()
  $expectedHash = $expectedHashes[$fileName]

  if ($actualHash -ne $expectedHash) {
    throw "SHA256 mismatch for '${fileName}'. Expected ${expectedHash}, got ${actualHash}."
  }

  Write-Output "# Verified SHA256 for '${fileName}'."
}

function Copy-RuntimeFiles {
  param([Parameter(Mandatory=$true)] [hashtable] $platform)

  $platformName = $platform.Name
  $sourceTorDirectory = Join-Path "Extracted" $platformName "tor"
  $destinationDirectory = Join-Path "Tor" $platformName
  New-Item -Path $destinationDirectory -ItemType Directory -Force | Out-Null

  foreach ($runtimeFile in $platform.RuntimeFiles) {
    $sourcePath = Join-Path $sourceTorDirectory $runtimeFile
    if (!(Test-Path -LiteralPath $sourcePath)) {
      throw "Extracted runtime file missing: ${sourcePath}"
    }

    Copy-Item -LiteralPath $sourcePath -Destination $destinationDirectory -Force
  }

  $licensePath = Join-Path "Extracted" $platformName "docs" "tor.txt"
  if (!(Test-Path -LiteralPath $licensePath)) {
    throw "Extracted Tor license missing: ${licensePath}"
  }

  Copy-Item -LiteralPath $licensePath -Destination (Join-Path $destinationDirectory "LICENSE") -Force
  Set-LfLineEndings (Join-Path $destinationDirectory "LICENSE")
}

function Clear-TorDestination {
  param([Parameter(Mandatory=$true)] [string] $destinationDirectory)

  if (!(Test-Path -LiteralPath $destinationDirectory)) {
    New-Item -Path $destinationDirectory -ItemType Directory -Force | Out-Null
    return
  }

  Get-ChildItem -LiteralPath $destinationDirectory -Force |
    Where-Object { $_.Name -ne ".gitattributes" } |
    Remove-Item -Force -Recurse
}

function Set-LfLineEndings {
  param([Parameter(Mandatory=$true)] [string] $path)

  $resolvedPath = (Resolve-Path -LiteralPath $path).Path
  $content = [System.IO.File]::ReadAllText($resolvedPath)
  $content = $content -replace "\r?\n", "`n"
  [System.IO.File]::WriteAllText($resolvedPath, $content)
}

$prevPwd = $PWD
Set-Location -LiteralPath $PSScriptRoot | Out-Null
Write-Output "# Set PWD to '$PSScriptRoot'."

try {
  Assert-CommandExists "tar"

  $tempDirectory = Join-Path "temp" $version

  if (!$skipDownloading) {
    Remove-Item -LiteralPath $tempDirectory -Force -Recurse -ErrorAction SilentlyContinue
    New-Item -Path $tempDirectory -ItemType Directory -Force | Out-Null
    Set-Location -LiteralPath $tempDirectory | Out-Null

    Invoke-DownloadFile $checksumFileName
    Invoke-DownloadFile $checksumSignatureFileName
    Invoke-DownloadUri $signingKeyUri $signingKeyFileName

    foreach ($platform in $platforms) {
      Invoke-DownloadFile $platform.Package
    }
  } else {
    if (!(Test-Path -LiteralPath $tempDirectory)) {
      throw "Folder '${tempDirectory}' does not exist. Run the script again without -skipDownloading."
    }

    Set-Location -LiteralPath $tempDirectory | Out-Null
    Write-Output "# Skip downloading Tor Expert Bundles."
  }

  Assert-ChecksumSignature
  $expectedHashes = Get-ExpectedHashes

  foreach ($platform in $platforms) {
    Assert-FileHash $expectedHashes $platform.Package
  }

  if ($skipExtractingArchives) {
    Write-Output "# Skip extracting Tor Expert Bundles."
  } else {
    Remove-Item -LiteralPath "Extracted" -Force -Recurse -ErrorAction SilentlyContinue

    foreach ($platform in $platforms) {
      $extractDirectory = Join-Path "Extracted" $platform.Name
      New-Item -Path $extractDirectory -ItemType Directory -Force | Out-Null
      Invoke-CheckedCommand "tar" @("-xzf", $platform.Package, "-C", $extractDirectory)
      Copy-RuntimeFiles $platform
    }
  }

  if ($skipReplacingTorBinaries) {
    Write-Output "# Skip replacing Tor binaries."
  } else {
    foreach ($platform in $platforms) {
      $sourceDirectory = Join-Path "Tor" $platform.Name
      $destinationDirectory = Join-Path $PSScriptRoot $platform.Name "Tor"

      Write-Output "# Replace Tor binaries in folder '${destinationDirectory}'."
      Clear-TorDestination $destinationDirectory
      Copy-Item -Recurse -Force -Path (Join-Path $sourceDirectory "*") -Destination $destinationDirectory
    }
  }

  if ($skipReplacingGeoIpFiles) {
    Write-Output "# Skip replacing geoip files."
  } else {
    $geoIpSource = Join-Path "Extracted" "lin64" "data"
    $geoIpDestination = Join-Path -Resolve $PSScriptRoot ".." ".." "Tor" "Geoip"
    Write-Output "# Replace geoip files in folder '${geoIpDestination}'."
    Copy-Item -Force -Path (Join-Path $geoIpSource "geoip*") -Destination $geoIpDestination
    Set-LfLineEndings (Join-Path $geoIpDestination "geoip")
    Set-LfLineEndings (Join-Path $geoIpDestination "geoip6")
  }

  Write-Output "# Done."
}
finally {
  Set-Location -Path $prevPwd | Out-Null
}
