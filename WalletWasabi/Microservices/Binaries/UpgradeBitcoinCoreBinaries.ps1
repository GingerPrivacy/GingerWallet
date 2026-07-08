# Script downloads Bitcoin Core release archives, verifies release signatures and checksums,
# extracts bitcoind, and replaces the platform-specific binaries used by Ginger Wallet.
#
# This script verifies official Bitcoin Core release authenticity and integrity.
# It does not independently verify reproducible build attestations. Bitcoin Core documents
# reproducible build verification on its download page:
# https://bitcoincore.org/en/download/
# Release build attestations are published in bitcoin-core/guix.sigs. For release binaries,
# check the version-specific signed `all.SHA256SUMS` files:
# https://github.com/bitcoin-core/guix.sigs
#
# Examples:
# 1] .\UpgradeBitcoinCoreBinaries.ps1
# 2] .\UpgradeBitcoinCoreBinaries.ps1 -version "31.0"
# 3] .\UpgradeBitcoinCoreBinaries.ps1 -version "31.0" -skipDownloading

[CmdletBinding()]
param(
  [Parameter(Mandatory=$false)]$version = "31.0",
  [Parameter(Mandatory=$false)][Switch]$skipDownloading,
  [Parameter(Mandatory=$false)][Switch]$skipExtracting,
  [Parameter(Mandatory=$false)][Switch]$skipReplacingBitcoinCoreBinaries,
  [Parameter(Mandatory=$false)][Switch]$allowUnsafeSkipGpgVerification)

Set-StrictMode -Version 3
$ErrorActionPreference = "Stop"

$distUri = "https://bitcoincore.org/bin/bitcoin-core-${version}"

$minimumTrustedSignatures = 1
$trustedSignerKeys = @(
  @{
    Name = "fanquake"
    Fingerprint = "E777299FC265DD04793070EB944D35F9AC3DB76A"
    Uri = "https://raw.githubusercontent.com/bitcoin-core/guix.sigs/main/builder-keys/fanquake.gpg"
  },
  @{
    Name = "m3dwards"
    Fingerprint = "E86AE73439625BBEE306AAE6B66D427F873CB1A3"
    Uri = "https://raw.githubusercontent.com/bitcoin-core/guix.sigs/main/builder-keys/m3dwards.gpg"
  }
)

$packages = @(
  @{
    Runtime = "win-x64"
    Archive = "bitcoin-${version}-win64.zip"
    BinaryPath = "bitcoin-${version}/bin/bitcoind.exe"
    BinaryName = "bitcoind.exe"
  },
  @{
    Runtime = "linux-x64"
    Archive = "bitcoin-${version}-x86_64-linux-gnu.tar.gz"
    BinaryPath = "bitcoin-${version}/bin/bitcoind"
    BinaryName = "bitcoind"
  },
  @{
    Runtime = "osx-x64"
    Archive = "bitcoin-${version}-x86_64-apple-darwin.tar.gz"
    BinaryPath = "bitcoin-${version}/bin/bitcoind"
    BinaryName = "bitcoind"
  },
  @{
    Runtime = "osx-arm64"
    Archive = "bitcoin-${version}-arm64-apple-darwin.tar.gz"
    BinaryPath = "bitcoin-${version}/bin/bitcoind"
    BinaryName = "bitcoind"
  }
)

function Invoke-Download {
  param(
    [Parameter(Mandatory=$true)] [string] $Uri,
    [Parameter(Mandatory=$true)] [string] $OutFile
  )

  Write-Output "# Downloading ${Uri} ..."
  Invoke-WebRequest -Uri $Uri -OutFile $OutFile
}

function Get-ExpectedSha256 {
  param(
    [Parameter(Mandatory=$true)] [string] $Sha256SumsPath,
    [Parameter(Mandatory=$true)] [string] $FileName
  )

  $line = Get-Content -LiteralPath $Sha256SumsPath |
    Where-Object { $_ -match "\s\*$([regex]::Escape($FileName))$" -or $_ -match "\s$([regex]::Escape($FileName))$" } |
    Select-Object -First 1

  if ([string]::IsNullOrWhiteSpace($line)) {
    throw "Could not find SHA256SUMS entry for '${FileName}'."
  }

  return ($line -split "\s+")[0].ToUpperInvariant()
}

function Assert-Sha256 {
  param(
    [Parameter(Mandatory=$true)] [string] $FilePath,
    [Parameter(Mandatory=$true)] [string] $ExpectedHash
  )

  $actualHash = (Get-FileHash -LiteralPath $FilePath -Algorithm SHA256).Hash.ToUpperInvariant()
  if ($actualHash -ne $ExpectedHash) {
    throw "SHA256 mismatch for '${FilePath}'. Expected '${ExpectedHash}', got '${actualHash}'."
  }
}

function Invoke-GpgVerification {
  param(
    [Parameter(Mandatory=$true)] [string] $Sha256SumsPath,
    [Parameter(Mandatory=$true)] [string] $SignaturePath,
    [Parameter(Mandatory=$true)] [string] $GpgHomePath,
    [Parameter(Mandatory=$true)] [array] $TrustedSignerKeys,
    [Parameter(Mandatory=$true)] [int] $MinimumTrustedSignatures
  )

  $gpg = Get-Command "gpg" -ErrorAction SilentlyContinue
  if ($null -eq $gpg) {
    throw "gpg was not found. Install gpg, or rerun with -allowUnsafeSkipGpgVerification only if you accept unauthenticated downloads."
  }

  Remove-Item -LiteralPath $GpgHomePath -Force -Recurse -ErrorAction SilentlyContinue
  New-Item -ItemType Directory -Force -Path $GpgHomePath | Out-Null

  $trustedKeyDir = Join-Path (Split-Path -Parent $GpgHomePath) "trusted-keys"
  New-Item -ItemType Directory -Force -Path $trustedKeyDir | Out-Null

  $trustedFingerprints = @{}
  foreach ($trustedSignerKey in $TrustedSignerKeys) {
    $expectedFingerprint = Get-NormalizedFingerprint $trustedSignerKey.Fingerprint
    $trustedFingerprints[$expectedFingerprint] = $trustedSignerKey.Name

    $keyPath = Join-Path $trustedKeyDir "$($trustedSignerKey.Name).gpg"
    if (!(Test-Path -LiteralPath $keyPath -PathType Leaf)) {
      Invoke-Download -Uri $trustedSignerKey.Uri -OutFile $keyPath
    }

    & gpg --homedir $GpgHomePath --batch --import $keyPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
      throw "Failed to import trusted Bitcoin Core signer key '$($trustedSignerKey.Name)'."
    }
  }

  $importedKeyOutput = & gpg --homedir $GpgHomePath --batch --with-colons --fingerprint --list-keys 2>&1
  $importedFingerprints = @($importedKeyOutput | Where-Object { $_ -like "fpr:*" } | ForEach-Object { Get-NormalizedFingerprint (($_ -split ":")[9]) })

  foreach ($trustedFingerprint in $trustedFingerprints.Keys) {
    if (!$importedFingerprints.Contains($trustedFingerprint)) {
      throw "Trusted Bitcoin Core signer key '$($trustedFingerprints[$trustedFingerprint])' did not contain expected fingerprint '$trustedFingerprint'."
    }
  }

  $verifyOutput = & gpg --homedir $GpgHomePath --batch --status-fd 1 --verify $SignaturePath $Sha256SumsPath 2>&1
  $validFingerprints = @(
    $verifyOutput |
      Where-Object { $_ -match "^\[GNUPG:\] VALIDSIG\s+" } |
      ForEach-Object { Get-NormalizedFingerprint (($_ -split "\s+")[2]) } |
      Where-Object { $trustedFingerprints.ContainsKey($_) } |
      Select-Object -Unique
  )

  if ($validFingerprints.Count -lt $MinimumTrustedSignatures) {
    $prettyOutput = $verifyOutput -join [Environment]::NewLine
    throw "SHA256SUMS.asc was not signed by enough pinned Bitcoin Core signer keys. Expected at least ${MinimumTrustedSignatures}, got $($validFingerprints.Count). GPG output:${prettyOutput}"
  }

  foreach ($fingerprint in $validFingerprints) {
    Write-Output "# Trusted Bitcoin Core signature verified: $($trustedFingerprints[$fingerprint]) ($fingerprint)."
  }
}

function Get-NormalizedFingerprint {
  param([Parameter(Mandatory=$true)] [string] $Fingerprint)

  return ($Fingerprint -replace "\s", "").ToUpperInvariant()
}

function Expand-BitcoinArchive {
  param(
    [Parameter(Mandatory=$true)] [hashtable] $Package,
    [Parameter(Mandatory=$true)] [string] $DestinationRoot
  )

  $runtime = $Package.Runtime
  $archive = $Package.Archive
  $archivePath = Join-Path (Get-Location) $archive
  $destination = Join-Path $DestinationRoot $runtime

  Remove-Item -LiteralPath $destination -Force -Recurse -ErrorAction SilentlyContinue
  New-Item -ItemType Directory -Force -Path $destination | Out-Null

  Write-Host "# Extracting ${archive} ..."
  if ($archive.EndsWith(".zip", [StringComparison]::OrdinalIgnoreCase)) {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $destination -Force
  } else {
    tar -xzf $archivePath -C $destination
  }

  $binarySource = Join-Path $destination $Package.BinaryPath
  if (!(Test-Path -LiteralPath $binarySource -PathType Leaf)) {
    throw "Expected bitcoind binary was not found: '${binarySource}'."
  }

  return $binarySource
}

$prevPwd = $PWD
Set-Location -LiteralPath $PSScriptRoot | Out-Null
Write-Output "# Set PWD to '$PSScriptRoot'."

try {
  $workDir = Join-Path $PSScriptRoot "temp/$version"
  $extractDir = Join-Path $workDir "extracted"

  if (!$skipDownloading) {
    Remove-Item -LiteralPath $workDir -Force -Recurse -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $workDir | Out-Null
    Set-Location -LiteralPath $workDir | Out-Null

    Invoke-Download -Uri "$distUri/SHA256SUMS" -OutFile "SHA256SUMS"
    Invoke-Download -Uri "$distUri/SHA256SUMS.asc" -OutFile "SHA256SUMS.asc"

    foreach ($package in $packages) {
      Invoke-Download -Uri "$distUri/$($package.Archive)" -OutFile $package.Archive
    }
  } else {
    if (!(Test-Path -LiteralPath $workDir -PathType Container)) {
      throw "Folder '${workDir}' does not exist. Run the script again without -skipDownloading."
    }

    Set-Location -LiteralPath $workDir | Out-Null
    Write-Output "# Skip downloading Bitcoin Core archives."
  }

  if ($allowUnsafeSkipGpgVerification) {
    Write-Warning "Skipping GPG verification is unsafe. SHA256 checks only protect against corruption, not against a compromised download source."
  } else {
    Invoke-GpgVerification `
      -Sha256SumsPath "SHA256SUMS" `
      -SignaturePath "SHA256SUMS.asc" `
      -GpgHomePath (Join-Path $workDir "gnupg") `
      -TrustedSignerKeys $trustedSignerKeys `
      -MinimumTrustedSignatures $minimumTrustedSignatures
  }

  foreach ($package in $packages) {
    $expectedHash = Get-ExpectedSha256 -Sha256SumsPath "SHA256SUMS" -FileName $package.Archive
    Assert-Sha256 -FilePath $package.Archive -ExpectedHash $expectedHash
    Write-Output "# SHA256 verified for $($package.Archive)."
  }

  $binarySources = @{}
  if ($skipExtracting) {
    Write-Output "# Skip extracting Bitcoin Core archives."
    foreach ($package in $packages) {
      $binarySource = Join-Path $extractDir $package.Runtime
      $binarySource = Join-Path $binarySource $package.BinaryPath
      if (!(Test-Path -LiteralPath $binarySource -PathType Leaf)) {
        throw "Expected extracted binary was not found: '${binarySource}'."
      }
      $binarySources[$package.Runtime] = $binarySource
    }
  } else {
    Remove-Item -LiteralPath $extractDir -Force -Recurse -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $extractDir | Out-Null

    foreach ($package in $packages) {
      $binarySources[$package.Runtime] = Expand-BitcoinArchive -Package $package -DestinationRoot $extractDir
    }
  }

  if ($skipReplacingBitcoinCoreBinaries) {
    Write-Output "# Skip replacing Bitcoin Core binaries."
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
  Set-Location -LiteralPath $prevPwd | Out-Null
}
