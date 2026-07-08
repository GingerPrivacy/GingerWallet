# Updating Executables

Runtime folders use .NET Runtime Identifier names (`win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`). Release asset names are mapped separately by the packager, so public macOS ZIPs keep the `macOS-*` label and DMGs keep the existing `Ginger-{version}.dmg` / `Ginger-{version}-arm64.dmg` convention.

1. Replace executables. For Bitcoin Core, run `UpgradeBitcoinCoreBinaries.ps1`. For HWI, run `UpgradeHwiBinaries.ps1`.
2. Properties/Copy to Output: Copy if newer. (VSBUG: Sometimes Copy Always is needed. It's generally ok to do copy always then set it back to Copy if newer, so already running Bitcoin Core will not be tried to get recopied all the time.)
3. Make sure the Linux and the OSX binaries are executable:
	`git update-index --chmod=+x .\linux-x64\hwi`
	`git update-index --chmod=+x .\osx-x64\hwi`
	`git update-index --chmod=+x .\linux-x64\bitcoind`
	`git update-index --chmod=+x .\osx-x64\bitcoind`
	`git update-index --chmod=+x .\osx-arm64\bitcoind`
	`git update-index --chmod=+x .\osx-arm64\hwi`
	`git update-index --chmod=+x .\osx-arm64\Tor\tor`
	`git update-index --chmod=+x .\linux-x64\Tor\tor`
	`git update-index --chmod=+x .\win-x64\Tor\tor.exe`
	`git update-index --chmod=+x .\osx-x64\Tor\tor`
4. Update the binary hashes of each executable and the text documentation in `*BinaryHashesTests.cs` test files.
5. Commit, push.
6. Make sure CI passes.

## Bitcoin Core Release Verification

`UpgradeBitcoinCoreBinaries.ps1` verifies official Bitcoin Core release authenticity and integrity before replacing binaries. The deterministic build scope for upstream Bitcoin Core binaries is documented in `WalletWasabi.Documentation/Guides/DeterministicBuildGuide.md`.
