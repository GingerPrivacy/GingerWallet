# Guide for reproducible builds

[Reproducible builds](https://reproducible-builds.org/) provide an independently verifiable path from source code to binary code. This guide explains how to rebuild Ginger Wallet and compare the result with an official release.

## Artifact scope

Ginger publishes both portable archives and signed installers. Their verification boundaries are different:

| Artifact | Verification boundary |
| --- | --- |
| `Ginger-<version>-win-x64.zip` | Compare the complete extracted application payload, including both unsigned Ginger executables, file by file. |
| Linux and macOS portable ZIPs | The extracted application payload can be compared file by file. |
| Windows MSI (v2.0.26+) | The MSI and its two Ginger executables contain Authenticode signatures and timestamps. Verify the signatures and compare the application payload as described below; do not expect flat-file SHA-256 equality. |
| macOS DMG | Apple signing and notarization modify the application bundle. The signed DMG is not expected to be bit-for-bit identical to an unsigned local build. |

Archive metadata can differ even when every extracted file matches, so verification compares the extracted payload rather than the ZIP container bytes.

WalletScrutiny's [published Windows verification](https://walletscrutiny.com/desktop/gingerwallet/#verificationId=7c15706f45060e06464653ae0ba9537c936a4c29bb2a183d3f4d7a74d831945e) used `Ginger-2.0.25-win-x64.zip` with the attached `gingerwallet_build.sh` v0.7.7. That script compares ordinary SHA-256 hashes of every extracted file and **explicitly rejects MSI input**; it does not normalize Authenticode signatures. Keep using the unsigned Windows ZIP for this verification workflow. Signing only the MSI's executables does not change that ZIP.

The [Ginger product definition](https://github.com/WalletScrutiny/WalletScrutinyCom/blob/master/_desktop/gingerwallet.md) currently lists `Ginger-*.msi` for Windows, which conflicts with the published script's accepted inputs. The verification report records a local build-server metadata override to select `Ginger-*-win-x64.zip`. That metadata needs to select the ZIP when using this script; removing executable signatures from an MSI would not make its unsupported format work. Section 4 is a separate MSI payload check, not a claim that WalletScrutiny's script supports MSI verification. [Artifact-level verdicts](https://github.com/WalletScrutiny/WalletScrutinyCom/blob/master/docs/verifications.md#artifact-level-verdicts) must stay tied to the actual download tested.

Current development builds require the .NET 10 SDK pinned in `global.json`. Rebuilding an older release requires that release's recorded SDK and runtime; the v2.0.26 example below intentionally uses .NET 8.

## 1. Download the reference payload and match its toolchain

The following commands use **Windows PowerShell 5.1 (`powershell.exe`) on Windows 10 or later**, with [Git](https://git-scm.com/) installed. Run the snippets in the same session. Ginger's release packager builds all four portable targets on Windows; use that host OS even when comparing its Linux or macOS output. Publishing on another OS can change platform-dependent files, and running the packager on macOS enters its signing workflow.

Choose a new working directory outside any existing source checkout. Download the portable ZIP from [Ginger Wallet releases](https://github.com/GingerPrivacy/GingerWallet/releases), authenticate it using the release's signed checksums, and retain its SHA-256 for the verification report. The example uses v2.0.26:

```powershell
$ErrorActionPreference = "Stop"
$version = "2.0.26"
$workRoot = Join-Path $PWD "ginger-repro-$version"
New-Item -ItemType Directory $workRoot | Out-Null
$releaseUrl = "https://github.com/GingerPrivacy/GingerWallet/releases/download/v$version"
$zip = Join-Path $workRoot "Ginger-$version-win-x64.zip"
Invoke-WebRequest "$releaseUrl/Ginger-$version-win-x64.zip" -OutFile $zip -UseBasicParsing
Get-FileHash -LiteralPath $zip -Algorithm SHA256
$officialRoot = Join-Path $workRoot "official-win-x64"
Expand-Archive -LiteralPath $zip -DestinationPath $officialRoot
$buildInfo = Get-Content (Join-Path $officialRoot "BUILDINFO.json") -Raw | ConvertFrom-Json
$buildInfo
```

`NetSdkVersion` identifies the SDK; `NetRuntimeVersion` identifies the runtime **executing the packager**, which writes `BUILDINFO.json`. It is not the self-contained runtime version shipped in the application. For example, v2.0.26 records SDK `8.0.404` and packager runtime `8.0.29`, while the SDK publishes application runtime `8.0.11`. Do not override the application's runtime to match the packager runtime.

Install both recorded versions into an isolated directory using Microsoft's [dotnet-install script](https://learn.microsoft.com/dotnet/core/tools/dotnet-install-script). This avoids changing `global.json` or accidentally using a newer system SDK/runtime:

```powershell
$dotnetRoot = Join-Path $workRoot "dotnet"
$installScript = Join-Path $workRoot "dotnet-install.ps1"
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile $installScript -UseBasicParsing
& $installScript -Version $buildInfo.NetSdkVersion -InstallDir $dotnetRoot -NoPath
& $installScript -Runtime dotnet -Version $buildInfo.NetRuntimeVersion -InstallDir $dotnetRoot -NoPath
$env:DOTNET_ROOT = $dotnetRoot
$env:PATH = "$dotnetRoot;$env:PATH"
```

## 2. Build the release tag

Build the exact tag, not the latest branch or this documentation PR. Check that the tag's commit agrees with the downloaded artifact:

```powershell
$repoRoot = Join-Path $workRoot "GingerWallet"
git clone --depth 1 --branch "v$version" https://github.com/GingerPrivacy/GingerWallet.git $repoRoot
if ($LASTEXITCODE -ne 0) { throw "Clone failed." }
Set-Location $repoRoot
$commit = git rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $commit -ne $buildInfo.GitCommitHash) {
  throw "The release tag does not match BUILDINFO.json."
}
$sdk = dotnet --version
if ($LASTEXITCODE -ne 0 -or $sdk -ne $buildInfo.NetSdkVersion) {
  throw "The selected SDK does not match BUILDINFO.json."
}

Set-Location (Join-Path $repoRoot "WalletWasabi.Packager")
dotnet restore --locked-mode
if ($LASTEXITCODE -ne 0) { throw "Restore failed." }
dotnet build --no-restore
if ($LASTEXITCODE -ne 0) { throw "Packager build failed." }
dotnet --fx-version $buildInfo.NetRuntimeVersion `
  bin\Debug\net8.0\WalletWasabi.Packager.dll --onlybinaries
if ($LASTEXITCODE -ne 0) { throw "Payload build failed." }
$builtRoot = Join-Path $repoRoot "WalletWasabi.Fluent.Desktop\bin\dist\win-x64"
```

The output directories are under `WalletWasabi.Fluent.Desktop\bin\dist`:

```text
win-x64
linux-x64
osx-x64
osx-arm64
```

`--onlybinaries` stops before installer creation and code signing. These directories are the unsigned reference payloads. Keep the checkout unmodified; do not accept a packager warning about uncommitted source changes without investigating it. SDK selection above does not require modifying or committing `global.json`.

## 3. Compare portable archives

Compare exact relative paths and SHA-256 values for **every** extracted file, including `BUILDINFO.json`, hidden files, and both Ginger executables. This does not use Git's text conversion or line-ending normalization:

```powershell
function Get-PayloadManifest([string] $root, [string[]] $exclude = @()) {
  $root = (Get-Item -LiteralPath $root).FullName.TrimEnd('\') + '\'
  $files = @(Get-ChildItem -LiteralPath $root -Recurse -File -Force)
  if ($files.Count -eq 0) { throw "Empty payload: $root" }
  foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($root.Length)
    if ($relativePath -cnotin $exclude) {
      [pscustomobject]@{
        RelativePath = $relativePath
        SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
      }
    }
  }
}

$builtManifest = @(Get-PayloadManifest $builtRoot)
$officialManifest = @(Get-PayloadManifest $officialRoot)
$payloadDiff = Compare-Object $builtManifest $officialManifest `
  -Property RelativePath, SHA256 -CaseSensitive
if ($payloadDiff) {
  $payloadDiff | Format-Table
  throw "Portable payload differs from the release-tag build."
}
Write-Output "All $($builtManifest.Count) portable payload files match exactly."
```

Do not exclude a differing `BUILDINFO.json`: check the SDK, packager runtime and source commit instead. For the other portable archives, repeat the comparison with the corresponding directory: `linux-x64` for `Ginger-<version>-linux-x64.zip`, `osx-x64` for `Ginger-<version>-macOS-x64.zip`, or `osx-arm64` for `Ginger-<version>-macOS-arm64.zip`.

## 4. Independently verify the signed Windows MSI

This supplementary procedure is independent of WalletScrutiny's published `gingerwallet_build.sh` v0.7.7, which rejects MSI files. It does not replace the ordinary SHA-256 comparison of the unsigned ZIP in section 3.

Starting with v2.0.26, the release process creates the portable ZIP first, signs `wassabee.exe` and `wassabeed.exe`, builds the MSI from that signed directory, and finally signs the MSI itself. Authenticode embeds a certificate and a timestamp in each signed file, so independently built unsigned files cannot have the same flat-file SHA-256 value.

First verify the MSI and extract its payload without installing it:

```powershell
$msi = Join-Path $workRoot "Ginger-$version.msi"
Invoke-WebRequest "$releaseUrl/Ginger-$version.msi" -OutFile $msi -UseBasicParsing
Get-FileHash -LiteralPath $msi -Algorithm SHA256
$signature = Get-AuthenticodeSignature -LiteralPath $msi
if ($signature.Status -ne "Valid") {
  throw "Invalid MSI Authenticode signature: $($signature.StatusMessage)"
}
$msiSigner = $signature.SignerCertificate.Thumbprint
$signature.SignerCertificate | Format-List Subject, Thumbprint

$extractRoot = Join-Path $workRoot "official-msi"
New-Item -ItemType Directory $extractRoot | Out-Null
$extraction = Start-Process msiexec.exe -Wait -PassThru -WindowStyle Hidden -ArgumentList @(
  "/a", "`"$msi`"", "/qn", "TARGETDIR=`"$extractRoot`""
)
if ($extraction.ExitCode -ne 0) {
  throw "MSI extraction failed with exit code $($extraction.ExitCode)."
}
```

Authenticate the MSI against the release's signed checksums too, and confirm that the displayed certificate identifies the expected release publisher. Administrative extraction (`msiexec /a`) can require an elevated PowerShell session; it extracts the installer without installing Ginger. Stop on an extraction error, and retry into a new empty directory with the required Windows permissions.

Compare the extracted `GingerWallet` directory directly with the locally built `win-x64` directory. All files except the two Ginger executables must match exactly. Verify each executable's signature, require the same signer as the MSI, and compare its Authenticode image hash. Run this in Windows PowerShell, where the built-in `AppLocker` module is available:

```powershell
Import-Module AppLocker -ErrorAction Stop
$msiRoot = Join-Path $extractRoot "GingerWallet"
$signedFiles = @("wassabee.exe", "wassabeed.exe")

$payloadDiff = Compare-Object `
  @(Get-PayloadManifest $builtRoot $signedFiles) `
  @(Get-PayloadManifest $msiRoot $signedFiles) `
  -Property RelativePath, SHA256 -CaseSensitive
if ($payloadDiff) {
  $payloadDiff | Format-Table
  throw "Unsigned MSI payload files do not match the reproducible build."
}

function Get-AuthenticodeImageHash([string] $path) {
  $fileInfo = @(Get-AppLockerFileInformation -Path $path -ErrorAction Stop)
  if ($fileInfo.Count -ne 1 -or $null -eq $fileInfo[0].Hash) {
    throw "No AppLocker image hash returned for $path"
  }
  $hash = $fileInfo[0].Hash
  if ($hash.HashType -ne "SHA256" -or $hash.HashDataString -notmatch '^0x[0-9A-Fa-f]{64}$') {
    throw "No valid SHA-256 image hash returned for $path"
  }
  return $hash.HashDataString
}

foreach ($name in $signedFiles) {
  $built = Join-Path $builtRoot $name
  $signed = Join-Path $msiRoot $name

  $signature = Get-AuthenticodeSignature -LiteralPath $signed
  if ($signature.Status -ne "Valid") {
    throw "Invalid signature on ${name}: $($signature.StatusMessage)"
  }

  if ($signature.SignerCertificate.Thumbprint -ne $msiSigner) {
    throw "The signer of $name differs from the MSI signer."
  }

  $builtHash = Get-AuthenticodeImageHash $built
  $signedHash = Get-AuthenticodeImageHash $signed
  if ($builtHash -ne $signedHash) {
    throw "Authenticode image hash mismatch for ${name}."
  }
  Write-Output "${name}: valid signature and matching image hash $builtHash"
}

Write-Output "MSI payload matches the reproducible build modulo valid Authenticode signatures."
```

Windows calculates the Authenticode image hash without the PE checksum, certificate-table entry, or certificate table. Consequently, the hash remains stable when a signature or timestamp is added or removed while still covering the executable image. See Microsoft's documentation on [Authenticode/PE image hashes](https://learn.microsoft.com/windows/security/application-security/application-control/app-control-for-business/design/select-types-of-rules-to-create#more-information-about-hashes).

This procedure verifies that the signed executables contain the reproducible application image. It does **not** make the MSI itself bit-for-bit reproducible. In addition to signatures and timestamps, the WiX project generates installer identifiers while building the package.

## 5. Record the result for WalletScrutiny

Record the version, source commit, host OS, both toolchain versions, commands and comparison output. For `gingerwallet_build.sh` v0.7.7, supply `Ginger-<version>-win-x64.zip` with `--arch x86_64-windows --type standalone`, or let the script select the architecture-specific ZIP. Do not pass an MSI: the script exits before building when `--binary` names an `.msi` file. Its build step dispatches `gingerwallet-build.yml` on `xrviv/WalletScrutinyCom` and requires appropriate GitHub access; the local procedure above does not require access to that workflow.

Identify each checked artifact by its original filename and **official download SHA-256**. WalletScrutiny's artifact hashes identify the downloaded files, not a newly built ZIP or a normalized executable. Any separate MSI check from section 4 must name the MSI's own hash and explicitly describe the different comparison method.

Report portable payload equality and MSI payload equality modulo validated Authenticode signatures separately. Do not report raw MSI byte equality, or extend a Windows result to untested Linux/macOS artifacts. WalletScrutiny decides how to classify the submitted evidence; publishing this guide does not change its artifact selection or verdict.

## 6. Signed macOS artifacts

Apple stores code signatures inside Mach-O binaries and the application bundle. Gatekeeper requires distributed applications to be signed and notarized, so the official DMG is expected to differ from an unsigned local build. Use the portable macOS ZIP for reproducible payload comparison, and use Apple's `codesign` and `spctl` tools to validate the distributed signed application.

## Bitcoin Core bundled binaries

Ginger bundles upstream Bitcoin Core `bitcoind` binaries as microservice runtime artifacts. Ginger does not independently rebuild Bitcoin Core release binaries as part of its deterministic build process.

`WalletWasabi/Microservices/Binaries/UpgradeBitcoinCoreBinaries.ps1` verifies official Bitcoin Core release authenticity and integrity before replacing bundled binaries:

1. Downloads release archives, `SHA256SUMS`, and `SHA256SUMS.asc` from `https://bitcoincore.org/bin/bitcoin-core-<version>/`.
2. Verifies `SHA256SUMS.asc` with pinned Bitcoin Core release signer fingerprints.
3. Verifies each downloaded archive against the signed `SHA256SUMS` entry.
4. Extracts only `bitcoind` / `bitcoind.exe` into the platform microservice folders.

This release verification is separate from independently proving that Bitcoin Core's published binaries are reproducible builds. Bitcoin Core documents that additional verification path under "Additional verification with reproducible builds" on the official download page:

https://bitcoincore.org/en/download/

Bitcoin Core reproducible build attestations are published in the `bitcoin-core/guix.sigs` repository:

https://github.com/bitcoin-core/guix.sigs

For website release binaries, the relevant per-release files are the signed `all.SHA256SUMS` files under the version-specific builder directories, for example:

```text
<version>/<builder>/all.SHA256SUMS
```

Those attestations can be used to verify that independent Guix builders reproduced the same release artifact hashes as the official website binaries. For Ginger client packaging, this deterministic build boundary is intentionally upstream: use the Bitcoin Core documentation and `bitcoin-core/guix.sigs` attestations as the source of truth for independent Bitcoin Core reproducible build verification.
