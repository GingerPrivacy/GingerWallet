# Script downloads Hardware Wallet Interface (HWI) release archives, verifies
# the signed checksum file, extracts hwi, and replaces the platform-specific
# binaries used by Ginger Wallet.
#
# Examples:
# 1] .\UpgradeHwiBinaries.ps1 -version "3.2.0"
# 2] .\UpgradeHwiBinaries.ps1 -version "3.2.0" -skipDownloading

[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)] [string] $version,
  [Parameter(Mandatory=$false)] [Switch] $skipDownloading,
  [Parameter(Mandatory=$false)] [Switch] $skipExtracting,
  [Parameter(Mandatory=$false)] [Switch] $skipReplacingHwiBinaries)

Set-StrictMode -Version 3
$ErrorActionPreference = "Stop"

if ($version -notmatch "^\d+\.\d+\.\d+(-rc\d+)?$") {
  throw "Invalid HWI version format: '${version}'."
}

$distUri = "https://github.com/bitcoin-core/HWI/releases/download/${version}"
$checksumFileName = "SHA256SUMS.txt.asc"
$signingKeyFingerprint = "152812300785C96444D3334D17565732E08E5E41"
$signingKeyFileName = "achow101.pgp"
$signingKeyUri = "https://achow101.com/achow101.pgp"

$packages = @(
  @{ Runtime = "win-x64"; Archive = "hwi-${version}-windows-x86_64.zip"; BinaryPath = "hwi.exe"; BinaryName = "hwi.exe" },
  @{ Runtime = "linux-x64"; Archive = "hwi-${version}-linux-x86_64.tar.gz"; BinaryPath = "hwi"; BinaryName = "hwi" },
  @{ Runtime = "osx-x64"; Archive = "hwi-${version}-mac-x86_64.tar.gz"; BinaryPath = "hwi"; BinaryName = "hwi" },
  @{ Runtime = "osx-arm64"; Archive = "hwi-${version}-mac-arm64.tar.gz"; BinaryPath = "hwi"; BinaryName = "hwi" }
)

function Assert-CommandExists {
  param([Parameter(Mandatory=$true)] [string] $command)
  if ($null -eq (Get-Command $command -ErrorAction SilentlyContinue)) {
    throw "Required command '$command' was not found on PATH."
  }
}

function Invoke-CheckedCommand {
  param([Parameter(Mandatory=$true)] [string] $command, [Parameter(Mandatory=$true)] [string[]] $arguments)
  Write-Host "# Running: $command $($arguments -join ' ')"
  & $command @arguments
  if ($LASTEXITCODE -ne 0) {
    throw "Command '$command' failed with exit code $LASTEXITCODE."
  }
}

function Invoke-Download {
  param([Parameter(Mandatory=$true)] [string] $uri, [Parameter(Mandatory=$true)] [string] $outFile)
  Write-Output "# Downloading ${uri} ..."
  Invoke-WebRequest -Uri $uri -OutFile $outFile
}

function Get-NormalizedFingerprint {
  param([Parameter(Mandatory=$true)] [string] $fingerprint)
  return ($fingerprint -replace "\s", "").ToUpperInvariant()
}

function Assert-SigningKeyFingerprint {
  param([Parameter(Mandatory=$true)] [string] $keyFilePath)
  $expectedFingerprint = Get-NormalizedFingerprint $signingKeyFingerprint
  $keyInfo = & gpg --batch --with-colons --show-keys $keyFilePath
  if ($LASTEXITCODE -ne 0) {
    throw "Failed to read HWI signing key."
  }

  $fingerprints = @(
    $keyInfo |
      Where-Object { $_.StartsWith("fpr:") } |
      ForEach-Object { Get-NormalizedFingerprint ($_.Split(":")[9]) }
  )

  if (!$fingerprints.Contains($expectedFingerprint)) {
    throw "Downloaded HWI signing key does not contain expected fingerprint '${expectedFingerprint}'."
  }
}

function Assert-ChecksumSignature {
  param(
    [Parameter(Mandatory=$true)] [string] $checksumFilePath,
    [Parameter(Mandatory=$true)] [string] $signingKeyFilePath,
    [Parameter(Mandatory=$true)] [string] $gpgHomePath)

  Assert-SigningKeyFingerprint $signingKeyFilePath
  Remove-Item -LiteralPath $gpgHomePath -Force -Recurse -ErrorAction SilentlyContinue
  New-Item -ItemType Directory -Force -Path $gpgHomePath | Out-Null
  Invoke-CheckedCommand "gpg" @("--homedir", $gpgHomePath, "--batch", "--import", $signingKeyFilePath)

  $verifyOutput = & gpg --homedir $gpgHomePath --batch --status-fd 1 --verify $checksumFilePath 2>&1
  if ($LASTEXITCODE -ne 0) {
    throw "GPG verification of '${checksumFilePath}' failed. GPG output:$($verifyOutput -join [Environment]::NewLine)"
  }

  $expectedFingerprint = Get-NormalizedFingerprint $signingKeyFingerprint
  $validFingerprints = @(
    $verifyOutput |
      Where-Object { $_ -match "^\[GNUPG:\] VALIDSIG\s+" } |
      ForEach-Object { Get-NormalizedFingerprint (($_ -split "\s+")[-1]) } |
      Select-Object -Unique
  )

  if (!$validFingerprints.Contains($expectedFingerprint)) {
    throw "HWI checksum file was not signed by pinned signer '${expectedFingerprint}'."
  }

  Write-Output "# Trusted HWI signature verified: ${expectedFingerprint}."
}

function Get-ExpectedSha256 {
  param([Parameter(Mandatory=$true)] [string] $sha256SumsPath, [Parameter(Mandatory=$true)] [string] $fileName)
  $line = Get-Content -LiteralPath $sha256SumsPath |
    Where-Object { $_ -match "^(?<hash>[a-fA-F0-9]{64})\s+\*?$([regex]::Escape($fileName))\s*$" } |
    Select-Object -First 1

  if ([string]::IsNullOrWhiteSpace($line)) {
    throw "Could not find SHA256SUMS entry for '${fileName}'."
  }

  return ($line -split "\s+")[0].ToUpperInvariant()
}

function Assert-Sha256 {
  param([Parameter(Mandatory=$true)] [string] $filePath, [Parameter(Mandatory=$true)] [string] $expectedHash)
  $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToUpperInvariant()
  if ($actualHash -ne $expectedHash) {
    throw "SHA256 mismatch for '${filePath}'. Expected '${expectedHash}', got '${actualHash}'."
  }
}

function Expand-HwiArchive {
  param([Parameter(Mandatory=$true)] [hashtable] $package, [Parameter(Mandatory=$true)] [string] $destinationRoot)
  $destination = Join-Path $destinationRoot $package.Runtime
  Remove-Item -LiteralPath $destination -Force -Recurse -ErrorAction SilentlyContinue
  New-Item -ItemType Directory -Force -Path $destination | Out-Null

  $archivePath = Join-Path (Get-Location) $package.Archive
  Write-Host "# Extracting $($package.Archive) ..."
  if ($package.Archive.EndsWith(".zip", [StringComparison]::OrdinalIgnoreCase)) {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $destination -Force
  } else {
    Invoke-CheckedCommand "tar" @("-xzf", $archivePath, "-C", $destination)
  }

  $binarySource = Join-Path $destination $package.BinaryPath
  if (!(Test-Path -LiteralPath $binarySource -PathType Leaf)) {
    throw "Expected HWI binary was not found: '${binarySource}'."
  }

  return $binarySource
}

$prevPwd = $PWD
Set-Location -LiteralPath $PSScriptRoot | Out-Null
Write-Output "# Set PWD to '$PSScriptRoot'."

try {
  Assert-CommandExists "tar"
  Assert-CommandExists "gpg"

  $workDir = Join-Path $PSScriptRoot "temp/hwi-${version}"
  $extractDir = Join-Path $workDir "extracted"
  $gpgHomePath = Join-Path $workDir "gnupg"

  if (!$skipDownloading) {
    Remove-Item -LiteralPath $workDir -Force -Recurse -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $workDir | Out-Null
    Set-Location -LiteralPath $workDir | Out-Null
    Invoke-Download -uri "$distUri/$checksumFileName" -outFile $checksumFileName
    Invoke-Download -uri $signingKeyUri -outFile $signingKeyFileName
    foreach ($package in $packages) {
      Invoke-Download -uri "$distUri/$($package.Archive)" -outFile $package.Archive
    }
  } else {
    if (!(Test-Path -LiteralPath $workDir -PathType Container)) {
      throw "Folder '${workDir}' does not exist. Run the script again without -skipDownloading."
    }
    Set-Location -LiteralPath $workDir | Out-Null
    Write-Output "# Skip downloading HWI archives."
  }

  Assert-ChecksumSignature -checksumFilePath $checksumFileName -signingKeyFilePath $signingKeyFileName -gpgHomePath $gpgHomePath

  foreach ($package in $packages) {
    $expectedHash = Get-ExpectedSha256 -sha256SumsPath $checksumFileName -fileName $package.Archive
    Assert-Sha256 -filePath $package.Archive -expectedHash $expectedHash
    Write-Output "# SHA256 verified for $($package.Archive)."
  }

  $binarySources = @{}
  if ($skipExtracting) {
    Write-Output "# Skip extracting HWI archives."
    foreach ($package in $packages) {
      $binarySource = Join-Path (Join-Path $extractDir $package.Runtime) $package.BinaryPath
      if (!(Test-Path -LiteralPath $binarySource -PathType Leaf)) {
        throw "Expected extracted binary was not found: '${binarySource}'."
      }
      $binarySources[$package.Runtime] = $binarySource
    }
  } else {
    Remove-Item -LiteralPath $extractDir -Force -Recurse -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $extractDir | Out-Null
    foreach ($package in $packages) {
      $binarySources[$package.Runtime] = Expand-HwiArchive -package $package -destinationRoot $extractDir
    }
  }

  if ($skipReplacingHwiBinaries) {
    Write-Output "# Skip replacing HWI binaries."
  } else {
    foreach ($package in $packages) {
      $runtimeDir = Join-Path $PSScriptRoot $package.Runtime
      $destination = Join-Path $runtimeDir $package.BinaryName
      New-Item -ItemType Directory -Force -Path $runtimeDir | Out-Null
      Copy-Item -LiteralPath $binarySources[$package.Runtime] -Destination $destination -Force
      Write-Output "# Replaced ${destination}."
    }
  }

  Write-Output "# Done."
}
finally {
  if (Test-Path -LiteralPath $gpgHomePath) {
    Remove-Item -LiteralPath $gpgHomePath -Force -Recurse -ErrorAction SilentlyContinue
  }

  Set-Location -LiteralPath $prevPwd | Out-Null
}
