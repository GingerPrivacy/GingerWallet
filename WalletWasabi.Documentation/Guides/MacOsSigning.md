# macOS signing, notarization and machine recovery

Use this guide to set up a release Mac from scratch or replace a lost machine.
All commands below assume a checkout of the intended release revision and a
Terminal opened in its root directory. Replace angle-bracket placeholders before
running commands. No real passwords, private keys or personal account IDs belong
in this document or the repository.

## What must be restored

Signing and notarization use **two independent credentials**:

| Item | Purpose | Where to create or restore it |
| --- | --- | --- |
| Developer ID Application certificate **and private key** | `codesign` signs the app and DMG | Apple Developer portal; import a protected `.p12` backup into Keychain Access, or issue a replacement certificate |
| App Store Connect **Team API Key** (`.p8`, Key ID, Issuer ID) | `notarytool` authenticates to Apple's notarization service | App Store Connect > Users and Access > Integrations > App Store Connect API > Team Keys |
| Team ID | Selects the signing team; not a password | Apple Developer account > Membership details |
| Local configuration and Keychain profile | Connects the scripts to the credentials | Recreate using the templates below |

The API key is independent of the Apple Account password. Resetting the account
password revokes app-specific passwords, which is why API authentication is used
for this workflow. It does not remove the need for an active Developer Program
membership, valid signing certificate, accepted agreements or a non-revoked API key.
See [Apple's app-specific password rules](https://support.apple.com/102654) and
[notarytool authentication](https://developer.apple.com/documentation/technotes/tn3147-migrating-to-the-latest-notarization-tool).

## Install the tools

1. Install Git and the .NET SDK required by the checkout's `global.json` (including its roll-forward policy).
2. Install Xcode or a Command Line Tools installation providing `notarytool` and `stapler`. Open Xcode and complete any requested first-launch setup and license acceptance.
3. Check `xcode-select -p`. If the wrong developer directory is selected, select your installation, for example `sudo xcode-select --switch /Applications/Xcode.app/Contents/Developer`.
4. Run `dotnet --version`, `xcrun --find notarytool` and `xcrun --find stapler` from the checkout. Resolve missing tools before continuing.

## Restore or create the signing identity

If a backup exists, import the password-protected `.p12` into the **login**
keychain using Keychain Access. Enter its password in the import dialog. Under
**My Certificates**, expanding `Developer ID Application: ... (<TEAM_ID>)` must
show its private key. A downloaded `.cer` by itself cannot restore a lost private key.

If no usable private key survives:

1. In Keychain Access, use **Certificate Assistant > Request a Certificate From a Certificate Authority** and save a CSR to disk. This creates a private key on the new Mac.
2. Sign in to [Certificates, Identifiers & Profiles](https://developer.apple.com/account/resources/certificates/list), create a **Developer ID Application** certificate and upload the CSR. Use the release team's membership. A Developer ID Installer certificate is for `.pkg` installers, not this app/DMG workflow.
3. Download the certificate and import it on the Mac that generated the CSR. Confirm that its private key is present.
4. Export the identity with its private key as a password-protected `.p12`. Put it in the team's encrypted backup storage, with the password in a password manager.

Use [Apple's certificate creation instructions](https://developer.apple.com/help/account/certificates/create-developer-id-certificates/)
and [Keychain import/export instructions](https://support.apple.com/guide/keychain-access/kyca35961/mac).
This guide does not use the legacy `Content/Osx/addcert.sh`, which contains fixed
password assumptions and broad key-access settings.

Check the installed identity:

```sh
security find-identity -v -p codesigning
```

The runner requires exactly one valid `Developer ID Application` identity for
the configured team. Resolve expired or duplicate identities in Keychain Access.
Keep the keychain unlocked during signing and approve legitimate `codesign`
private-key access prompts. The identity listing alone does not test key access.

## Create or restore the notarization API key

1. Sign in to [App Store Connect](https://appstoreconnect.apple.com/access/integrations/api) as the team's Account Holder or Admin.
2. Open **Users and Access > Integrations > App Store Connect API > Team Keys**. Request API access if required.
3. Generate a key named, for example, `Ginger Notarization`, with **Developer** access.
4. Record the **Key ID** and **Issuer ID**. The Issuer ID is not the 10-character Developer Team ID.
5. Download `AuthKey_<KEY_ID>.p8`. Apple allows the private key to be downloaded only once. Store an encrypted off-device backup with its identifiers.

Follow [Apple's API-key guide](https://developer.apple.com/help/app-store-connect/get-started/app-store-connect-api/).
This runbook uses a **Team Key**, including for an Individual Developer Program
membership; it does not use the Individual Keys tab.

Restore the `.p8` from backup on a replacement Mac, or generate a new key if it is
lost. Place it outside the repository and register it with `notarytool`:

```sh
install -d -m 700 "$HOME/.config/ginger-notarization"
install -m 600 "$HOME/Downloads/AuthKey_<KEY_ID>.p8" "$HOME/.config/ginger-notarization/AuthKey_<KEY_ID>.p8"
xcrun notarytool store-credentials WasabiNotarizeAPI \
  --key "$HOME/.config/ginger-notarization/AuthKey_<KEY_ID>.p8" \
  --key-id "<KEY_ID>" \
  --issuer "<ISSUER_ID>"
xcrun notarytool history --keychain-profile WasabiNotarizeAPI --output-format json
```

Both commands must succeed. `store-credentials` validates before saving; do not
use `--no-validate` to bypass authentication errors. A Keychain profile is local
configuration, not a substitute for off-device backups. To rotate keys without
mixing password/API credentials, use a new profile name and update the config.

## Local configuration template

Copy the [versioned configuration template](../../WalletWasabi.Packager/scripts/macos-signing.conf.example):

```sh
install -d -m 700 "$HOME/.config/ginger-notarization"
install -m 600 WalletWasabi.Packager/scripts/macos-signing.conf.example \
  "$HOME/.config/ginger-notarization/config.sh"
```

Edit the copy to contain your Team ID and chosen Keychain profile:

```sh
export GINGER_SIGNING_TEAM_ID="<TEAM_ID>"
export GINGER_NOTARY_KEYCHAIN_PROFILE="WasabiNotarizeAPI"
# Optional:
# export GINGER_SIGNING_LOG_DIR="$HOME/Library/Logs/GingerWallet"
```

This is a trusted local shell file, sourced by the runner. It contains no password
or private-key contents. Set `GINGER_MACOS_SIGNING_CONFIG` to use a different file;
the same exported settings can also be supplied directly without a file.

## Validate and run

```sh
bash WalletWasabi.Packager/scripts/sign-macos.sh --check
bash WalletWasabi.Packager/scripts/sign-macos.sh
```

`--check` validates the SDK, Apple tools, signing identity and notarization profile.
It needs network access but does not sign, upload, delete or package anything.
It does not test a full signing operation, input archives or DMG layout assets.

For a real release, connect exactly one mounted drive whose name contains `USB`
(case-sensitive). Its root must contain exactly these two matching-version inputs:

```text
GingerToNotarize-<VERSION>.zip
GingerToNotarize-<VERSION>-arm64.zip
```

Use the Windows release packager outputs. Also ensure the release DMG layout assets
exist under `WalletWasabi.Packager/Content/Osx/Dmg`, including `.DS_Store.dat` and
any referenced background image. Preserve those assets in release backups if they
are not supplied by the checkout; see the layout instructions below.

The runner uses the checkout containing the script, loads local configuration,
writes a unique log under `~/Library/Logs/GingerWallet`, checks access and starts
the packager. Authentication failure stops the run; it never prompts for an Apple
Account password or silently switches to password authentication.

The packager signs the app, notarizes and staples it, then creates, signs,
notarizes and staples the DMG. Final `Ginger-<VERSION>.dmg` and
`Ginger-<VERSION>-arm64.dmg` files are moved to the USB. Successful inputs are
deleted, existing same-name output DMGs on the USB are replaced, and the working
directory `~/Desktop/gingerTemp` is removed/recreated during packaging. Keep source
archives elsewhere and run only one release packager at a time.

For legacy direct invocations, `appleid=<EMAIL>:<TEAM_ID>` is still accepted to
select the signing team. The email does not authenticate notarization. Direct
packager invocations without `GINGER_NOTARY_KEYCHAIN_PROFILE` retain the legacy
`WasabiNotarize` profile default; the new runner defaults to `WasabiNotarizeAPI`.

## Optional Desktop launcher template

The Desktop launcher contains only a checkout path and a call to the versioned
runner. The [AppleScript source](../../WalletWasabi.Packager/scripts/WasabiNoratize.applescript)
is the template; the historical misspelling in the filename is retained for compatibility.
It assumes the checkout is `~/Desktop/GingerWallet`. Adjust `repositoryPath` in
the template if needed, then compile it:

```sh
osacompile -o "$HOME/Desktop/WasabiNoratize.scpt" \
  WalletWasabi.Packager/scripts/WasabiNoratize.applescript
```

Run it from Script Editor, or with `osascript "$HOME/Desktop/WasabiNoratize.scpt"`.
Allow Terminal automation when macOS asks. No credential or release logic belongs
in this wrapper; later runner fixes are picked up from the checkout automatically.

## Lost-machine recovery checklist and record template

Keep an encrypted backup **off this Mac** of the `.p12`, its password, the `.p8`,
and any release-only DMG assets. Neither a local folder nor a Git commit preserves
private keys on another machine. Maintain Apple Account/2FA recovery access too.
Do not commit credential files; `.p8` and `.p12` are ignored as an extra safeguard.

Copy this record into the team's private recovery documentation and fill in the
locations, not the secret contents:

```text
Signing owner / Apple account: <ACCOUNT>
Developer Team ID: <TEAM_ID>
Developer ID Application certificate name / expiry: <NAME> / <DATE>
Encrypted .p12 backup location: <VAULT ITEM>
.p12 password location: <PASSWORD MANAGER ITEM>
API key name / access: Ginger Notarization / Developer (Team Key)
API Key ID: <KEY_ID>
API Issuer ID: <ISSUER_ID>
Encrypted .p8 backup location: <VAULT ITEM>
Keychain profile to recreate: WasabiNotarizeAPI
Local config: ~/.config/ginger-notarization/config.sh
Repository URL and release revision: <URL> / <COMMIT OR TAG>
DMG layout asset backup location: <LOCATION>
Last successful --check / complete release: <DATE> / <VERSION>
```

On the replacement Mac: install tools, clone the release revision, restore or
reissue the signing identity, restore or generate the API key, recreate its
Keychain profile and config, restore layout assets, run `--check`, regenerate the
optional Desktop launcher, then package and verify a release. No old Desktop
script or personal password is needed to reconstruct this workflow.

If the Mac was stolen or its secrets may be exposed, revoke the affected API key
in App Store Connect and provision a replacement. Review the Developer ID
certificate with Apple before revoking it because revocation can affect already
distributed software. If the signing private key is simply unavailable, create a
new CSR/certificate instead of importing only the old `.cer`.

## Troubleshooting

| Symptom | Next step |
| --- | --- |
| Missing Keychain profile | Re-run `store-credentials` in the current macOS user account. Check the configured profile name. |
| HTTP 401 | Check Key ID, Issuer ID, the matching `.p8`, key status and team. Recreate the profile after correcting them. Password reset recovery is not part of the API workflow. |
| Access/agreements error | Check Developer membership, App Store Connect agreements and API-key access. |
| No signing identity | Import the Developer ID Application certificate with its private key; check expiry and Team ID. |
| Multiple matching identities | Resolve the duplicate signing identities in Keychain Access. |
| Key access denied / interaction not allowed | Unlock the login keychain and allow `codesign` to access the signing key. |
| Missing SDK or Apple tool | Recheck `global.json`, `dotnet --version`, `xcode-select -p` and Xcode first-launch setup. |
| USB selection or ZIP count failure | Leave exactly one mounted volume containing `USB` in its name and the two matching input ZIPs at its root. |
| Missing DMG layout file | Restore the release layout assets under `Content/Osx/Dmg`. |
| Notarization returns Invalid | Use `xcrun notarytool log <SUBMISSION_ID> --keychain-profile <PROFILE>` and inspect Apple's report. |

`Accepted` plus successful staple/validate and Gatekeeper checks is the packaging
result to look for. Run the produced app on the target architecture as a separate
release smoke test; successful notarization alone is not a functional app test.

## Changing the icon

- Wild specificaiton for the icon:
  - 1024x1024 size
  - 98px padding
  - BoxShadow params: x:0, y:5, Blur:25, Spread:0, Color:000000, Transparency:50%
- https://github.com/zkSNACKs/WalletWasabi/issues/2951

## Changing the background of dmg installer

1. Get a Mac
2. Install create-dmg https://github.com/andreyvit/create-dmg 
3. Create a folder called somewhere called wasabidmg
4. Get `Wasabi Wallet.app` for example you can download the latest release and extract it from dmg(7zip). Then copy it under wasabidmg.
5. Copy the following two files under wasabidmg Logo_with_text_small.png and WasabiLogo.icns
6. Open terminal at wasabidmg
7. Set the version number in the following command and run it
9. `create-dmg --volname "Wasabi Wallet" --volicon "WasabiLogo.icns" --background "Logo_with_text_small.png" --window-pos 200 120 --window-size 600 440 --icon "Wasabi Wallet.app" 110 150 --app-drop-link 500 150 --hdiutil-verbose "Wasabi.dmg" "Wasabi Wallet.app/"`
11. Dmg file is created under wasabidmg
12. Copy the dmg to windows computer, extract(7zip) DS_Store file and copy it to `WalletWasabi\WalletWasabi.Packager\Content\Osx\Dmg\DS_Store.dat` - add the extension .dat to prevent macOs overwriting the file.
13. Copy the new background PNG to `WalletWasabi\WalletWasabi.Packager\Content\Osx\Dmg\.background`
14. Now you can run the packager procedure
