# Updating Executables

1. Replace executables. For Bitcoin Core, run `UpgradeBitcoinCoreBinaries.ps1`. For HWI, run `UpgradeHwiBinaries.ps1`.
2. Properties/Copy to Output: Copy if newer. (VSBUG: Sometimes Copy Always is needed. It's generally ok to do copy always then set it back to Copy if newer, so already running Bitcoin Core will not be tried to get recopied all the time.)
3. Make sure the Linux and the OSX binaries are executable:
	`git update-index --chmod=+x hwi`
	`git update-index --chmod=+x .\lin64\bitcoind`
	`git update-index --chmod=+x .\osx64\bitcoind`
	`git update-index --chmod=+x .\osx-arm64\bitcoind`
	`git update-index --chmod=+x .\osx-arm64\hwi`
	`git update-index --chmod=+x .\osx-arm64\Tor\tor`
	`git update-index --chmod=+x .\lin64\Tor\tor`
	`git update-index --chmod=+x .\win64\Tor\tor.exe`
	`git update-index --chmod=+x .\osx64\Tor\tor`
4. Update the binary hashes of each executable and the text documentation in `*BinaryHashesTests.cs` test files.
5. Commit, push.
6. Make sure CI passes.

## Bitcoin Core Release Verification

`UpgradeBitcoinCoreBinaries.ps1` verifies official Bitcoin Core release authenticity and integrity before replacing binaries. The deterministic build scope for upstream Bitcoin Core binaries is documented in `WalletWasabi.Documentation/Guides/DeterministicBuildGuide.md`.
