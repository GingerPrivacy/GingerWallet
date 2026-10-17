# Final tests

- Check the exact **date** of the last release and the **name** of the last PR.
- List the PR-s in order, open the [link and (adjust date!)](https://github.com/zkSNACKs/WalletWasabi/pulls?q=is%3Apr+merged%3A%3E%3D2019-07-07+sort%3Aupdated-asc).
- Go through all PR, create the Final Test issue. Create test cases according to PR-s and write a list - [Final Test format](https://github.com/zkSNACKs/WalletWasabi/issues/2227).
- Go through all issues and pick the [important ones (adjust date!)](https://github.com/zkSNACKs/WalletWasabi/issues?utf8=%E2%9C%93&q=is%3Aissue+closed%3A%3E%3D2019-07-07+sort%3Aupdated-asc+) and add to Final Tests if required.
- Check Tor status. Never release during a Tor network disruption: https://status.torproject.org/
- At the end there will be a Final Test document.
- Do testing contribution game if needed. 

# Release candidate

- Go to your own fork of Wasabi and press Draft a new release. Release candidates are not published in the main repository!
- Tag version: add `rc1` postfix e.g: v1.1.7rc1.
- Set release title e.g: `Wasabi v1.1.7: <Release Title> - Release candidate`.
- Add "Do not use this" to the release notes. 
- Set as a pre-release. Save Draft.
- Do Packaging (see below).
- Upload the files to the pre-release.
- Check `This is a pre-release` and press Publish Release.
- Add the pre-release link to the Final Test issue.
- Share the Final Test issue link with developers and test it for 24 hours.
- Every PR that is contained in the release must be at least 24 hours old.

Make sure to run a virus detection scan on one of the Release candidate's .msi installer (preferably the final one). You can use this site for example: https://www.virustotal.com/gui/home/upload.

# Packaging

- Make sure the local .NET Core version is up to date.
- Make sure CI and CodeFactor check out.
- Run tests.
- MAKE SURE YOU ARE ON THE RIGHT BRANCH AND UP TO DATE in GitHub Desktop on the release machine!
- Discard packages.lock changes if there are. Inserted USB drive name must contain the string USB! 
- Run the [script file](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Packager/scripts/Wasabi_release.ps1) on the **Windows Release Laptop** and follow the instructions.
- On the Mac, run `bash WalletWasabi.Packager/scripts/sign-macos.sh` or the Desktop launcher generated from its versioned AppleScript template. Follow the [macOS signing and recovery guide](../Guides/MacOsSigning.md) to install the Developer ID identity and API-key Keychain profile. Do not put Apple passwords or private keys in the launcher.
- Finish the script on Windows. Now a folder should pop up with all the files that need to be uploaded to GitHub.
- Verify the Windows Authenticode signatures before uploading:
  - `signtool.exe verify /pa /v Ginger-<version>.msi`
  - `signtool.exe verify /pa /v win-x64\wassabee.exe`
  - `signtool.exe verify /pa /v win-x64\wassabeed.exe`
- Test asc file for `.msi`.
- Final `.msi` test on own computer. Check the About dialog and optionally the BUILDINFO.json next to the wasabi executable, the commit ID should match with the one on GitHub. 

# Final release

- Draft a [new release at the main repo](https://github.com/zkSNACKs/WalletWasabi/releases/new).
- Bump client version. (WalletWasabi/Helpers/Constants.cs) - maybe you already did this.
- Create the release notes by using [the template](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/ClientRelease/ReleaseNotesTemplate.md). Make sure the the recent changes are in the What's new section. 
- Run tests.
- Do Packaging (see above).
- Upload the files to the main repo!
- Download MSI from the draft release to your local computer, test it, and verify the version number in about!
- Do not set pre-release flag!
- Publish the release.

# Notify

- Refresh website download and signature links.
- [Deploy testnet and mainnet backend](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/HowToDeploy.md). Make sure the client version number is bumped here as well. If it is a hotfix you do not need to update the backend, but you need to update the website!

# Announce

- [Twitter](https://twitter.com) (tag @wasabiwallet #Bitcoin #Privacy).
- Submit to [/r/WasabiWallet](https://old.reddit.com/r/WasabiWallet/) and [/r/Bitcoin](https://old.reddit.com/r/Bitcoin/).

# Backporting

Backport is a branch. It is used for creating silent releases (hotfixes, small improvements) on top of the last released version. For this reason it has to be maintained with special care.

## Merge PR into backport (release branch)

- There is a PR which is merged to master and selected to backport.
- Checkout the current backport branch to a new local branch like bp_whatever: `git checkout -b bp_whatever upstream/backport`.
- Go to the merged PR / Commits and copy the hash of the commit.
- Cherry-pick: `git cherry-pick 35c4db348__hash of the commit__06abcd9278c`.
- git push origin bp_whatever.
- Create a PR into upstream/backport and name it as [Backport] Whatever.

## Code signing certificate

Certum issues the Windows code-signing certificate for the "Open Source Developer Martin Rimóczi" publisher through SimplySign.

- Issuing CA: Certum Code Signing 2021 CA
- Platform: Microsoft Authenticode
- Type: Code Signing

The release script signs the Windows application executables before the MSI is built, then signs the final MSI after WiX finishes. The packager requires the certificate's SHA-1 thumbprint in the `GINGER_WINDOWS_SIGN_CERT_SHA1` environment variable. This SHA-1 value only selects the certificate; file signatures and RFC 3161 timestamps use SHA-256.

Before packaging:

1. Sign in to SimplySign Desktop and make sure the current, valid code-signing certificate is available.
2. Open the certificate, select the **Details** tab, and copy the **Thumbprint** value as described in [Certum's SignTool instructions](https://www.files.certum.eu/documents/manual_en/Signing_with_the_use_of_jarsigner_tool_and_signtool.pdf). Do not use the certificate serial number.
3. Set the thumbprint for the current PowerShell session:

```powershell
$env:GINGER_WINDOWS_SIGN_CERT_SHA1 = "<certificate-thumbprint>"
```

The packager ignores whitespace and common invisible direction marks copied by the Windows certificate viewer, but otherwise requires exactly 40 hexadecimal characters. To persist the value for future shells, run:

```powershell
setx GINGER_WINDOWS_SIGN_CERT_SHA1 "<certificate-thumbprint>"
```

`setx` only affects newly started processes, so open a new PowerShell window before running the release script.

SmartScreen reputation is controlled by Microsoft and can still show warnings for newly signed or low-reputation releases even when Authenticode verification succeeds. Keep the publisher identity consistent across certificate renewals and sign every Windows release so reputation can build over time.

## Packager environment setup

### WSL

You can disable WSL sudo password prompt with this oneliner: 

```
echo "`whoami` ALL=(ALL) NOPASSWD:ALL" | sudo tee /etc/sudoers.d/`whoami` && sudo chmod 0440 /etc/sudoers.d/`whoami`
```

Use WSL 1 otherwise you cannot enter anything to the console (sudo password, appleid). https://github.com/microsoft/WSL/issues/4424
