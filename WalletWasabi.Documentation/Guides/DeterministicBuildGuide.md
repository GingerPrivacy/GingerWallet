# Guide for deterministic builds

The term *deterministic builds* is [defined](https://reproducible-builds.org/) as follows:

> Reproducible [or deterministic] builds are a set of software development practices that create an independently-verifiable path from source to binary code.

This guide describes how to reproduce Wasabi's builds. If you get stuck with these instructions, take a look at [how to build Wasabi from source code](https://docs.wasabiwallet.io/using-wasabi/BuildSource.html).

**Warning:** Reproducible builds were introduced in [1.1.3 release](https://github.com/zkSNACKs/WalletWasabi/releases/tag/v1.1.3), you cannot use these instructions for older versions!

## 1. Assert correct environment

In order to reproduce Wasabi's builds, you need [git](https://git-scm.com/) package, Windows 10+, and the version of [.NET SDK](https://dotnet.microsoft.com/download) that was used by the Wasabi team to produce the release.

Which version of .NET SDK to use? There is the `BUILDINFO.json` file inside the installation folder: `C:\Program Files\WasabiWallet\`, and the `NetSdkVersion` field will tell you the right SDK version. If you have multiple .NET SDK versions installed on your system, make sure to specify the exact same version in `global.json` before building Wasabi Wallet. `global.json` is in the root of the repository folder.

Example of `global.json` that is set to strictly use a specific version:

```json
{
  "sdk": {
    "version": "7.0.100",
    "allowPrerelease": false,
    "rollForward": "disable"
  }
}
```

## 2. Reproduce builds

You can see the list of Wasabi releases here: https://github.com/zkSNACKs/WalletWasabi/releases. Please note that each release has a git tag assigned, which is useful in the following instructions:

```sh
# The following command downloads only a single git branch. However, you can clone the whole repository, which is bigger.
git clone --depth 1 --branch <git-branch-or-tag> https://github.com/zkSNACKs/WalletWasabi.git # where `<git-branch-or-tag>` may be, for example, `v1.1.11.1`.
cd WalletWasabi/WalletWasabi.Packager
dotnet nuget locals all --clear
dotnet restore
dotnet build
dotnet run -- --onlybinaries
```

The previous commands produce Wasabi's binaries for Windows, macOS and Linux. Also, for your convenience, a new file explorer window will navigate you to the binaries location - i.e. `WalletWasabi\\WalletWasabi.Fluent.Desktop\\bin\\dist`.

![](https://i.imgur.com/8XAQzz4.png)

## 3. Verify builds

Now, we will attempt to verify the binaries you have just compiled with the officially distributed binaries on https://wasabiwallet.io website. Please download those packages from the website, you should see the following files in your File Explorer:

![](https://i.imgur.com/aI9Kx0c.png)

### Windows

* Install Wasabi using `Wasabi-<version>.msi` file. It will install to `C:\Program Files\WasabiWallet` directory.
* Start `cmd` or Powershell and navigate to the `dist` directory.
* Execute the following command:
  ```sh
  git diff --no-index "win7-x64" "C:\Program Files\WasabiWallet"
  ```
* Make sure that there is **NO** difference reported by the command.

### Linux & macOS

You can use the [Windows Subsystem for Linux](https://docs.microsoft.com/en-us/windows/wsl/) to verify all the packages in one go. At the time of writing this guide we provide `.tar.gz` and `.deb` packages for Linux and `.dmg` package for macOS.  
Install the `.deb` package and extract the `tar.gz` and `.dmg` packages, then compare them with your build.

After [installing WSL](https://docs.microsoft.com/en-us/windows/wsl/install-win10), just type `wsl` in File Explorer where your downloaded and built packages are located.

![](https://i.imgur.com/yRUjxvG.png)

#### .deb

```sh
sudo dpkg -i Wasabi-1.1.6.deb
git diff --no-index linux-x64/ /usr/local/bin/wasabiwallet/
```

#### .tar.gz

```sh
tar -pxzf Wasabi-1.1.6.tar.gz
git diff --no-index linux-x64/ Wasabi-1.1.6
```

*There could be warnings regarding SOS_README.md that it differs in line endings. That is a text file and it has no effect on the running software.*

#### .dmg

According to Apple documentation, the signature that is used to ensure the integrity of the software is added into the binary itself - so it will manipulate the content of the files.

> If the code is universal, the object code for each slice (architecture) is signed separately. This signature is stored within the binary file itself.

[Source](https://developer.apple.com/library/archive/documentation/Security/Conceptual/CodeSigningGuide/AboutCS/AboutCS.html#//apple_ref/doc/uid/TP40005929-CH3-SW3)

According to this, it is impossible to have both deterministic build and code signature on macOS. macOS Gatekeeper won't let you run software without it. Thus, Wasabi only applies code signature, but no deterministic build for macOS. 

There is an issue [here](https://github.com/zkSNACKs/WalletWasabi/issues/4110) for further discussion. 

With the following method you can check the differences by yourself:

You will need to install `7z` (or something else) to extract the `.dmg`. You can do that using `sudo apt install p7zip-full` command.

```sh
7z x Wasabi-1.1.6.dmg -oWasabiOsx
git diff --no-index osx-x64/ WasabiOsx/Wasabi\ Wallet.App/Contents/MacOS/
```

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
