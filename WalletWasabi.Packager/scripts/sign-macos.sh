#!/bin/bash
set -euo pipefail

case "${1:-}" in
    ""|--check) ;;
    --help|-h)
        echo "Usage: bash sign-macos.sh [--check]"
        echo "--check validates tools, signing identity and notarization access without packaging."
        echo "Config: \${GINGER_MACOS_SIGNING_CONFIG:-\$HOME/.config/ginger-notarization/config.sh}"
        exit 0
        ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
esac
if [[ $# -gt 1 ]]; then
    echo "Expected at most one argument." >&2
    exit 2
fi
if [[ "$(uname -s)" != Darwin ]]; then
    echo "macOS signing requires macOS." >&2
    exit 1
fi

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
project_dir=$(cd "$script_dir/.." && pwd)
config_file=${GINGER_MACOS_SIGNING_CONFIG:-$HOME/.config/ginger-notarization/config.sh}
if [[ -f "$config_file" ]]; then
    # This is a trusted local shell configuration, not a downloaded release asset.
    source "$config_file"
fi
if [[ ! "${GINGER_SIGNING_TEAM_ID:-}" =~ ^[A-Z0-9]{10}$ ]]; then
    echo "Set GINGER_SIGNING_TEAM_ID to your 10-character Apple Developer Team ID." >&2
    echo "Copy scripts/macos-signing.conf.example to $config_file and fill it in." >&2
    exit 1
fi
export GINGER_SIGNING_TEAM_ID
export GINGER_NOTARY_KEYCHAIN_PROFILE=${GINGER_NOTARY_KEYCHAIN_PROFILE:-WasabiNotarizeAPI}
export PATH="/opt/homebrew/bin:/usr/local/bin:/usr/local/share/dotnet:$PATH"

previous_umask=$(umask)
umask 077
log_dir=${GINGER_SIGNING_LOG_DIR:-$HOME/Library/Logs/GingerWallet}
mkdir -p "$log_dir"
log_file=$(mktemp "$log_dir/notarize-$(date +%Y%m%d-%H%M%S)-XXXXXX")
exec > >(tee -a "$log_file") 2>&1
umask "$previous_umask"
trap 'result=$?; if [[ $result -ne 0 ]]; then echo "Stopped (exit $result). See WalletWasabi.Documentation/Guides/MacOsSigning.md."; fi; echo "Log: $log_file"' EXIT

cd "$project_dir"
for tool in dotnet xcrun security codesign hdiutil; do
    command -v "$tool" >/dev/null || { echo "Required tool is missing: $tool"; exit 1; }
done
# Running in the project directory checks the SDK selected by global.json.
dotnet --version
xcrun --find notarytool >/dev/null
xcrun --find stapler >/dev/null
identities=$(security find-identity -v -p codesigning)
matching_identities=$(printf '%s\n' "$identities" | grep -F 'Developer ID Application:' | grep -F "($GINGER_SIGNING_TEAM_ID)" || true)
identity_count=$(printf '%s\n' "$matching_identities" | awk 'NF { count++ } END { print count+0 }')
if [[ "$identity_count" != 1 ]]; then
    echo "Expected exactly one valid Developer ID Application identity for team $GINGER_SIGNING_TEAM_ID; found $identity_count."
    echo "Import the certificate AND its private key into Keychain Access; resolve duplicate identities before signing."
    exit 1
fi
echo "Developer ID signing identity found."
echo "Checking notarization profile: $GINGER_NOTARY_KEYCHAIN_PROFILE"
if ! auth_result=$(xcrun notarytool history --keychain-profile "$GINGER_NOTARY_KEYCHAIN_PROFILE" --output-format json 2>&1); then
    printf '%s\n' "$auth_result"
    echo "Restore or renew the API credentials using the macOS signing guide."
    exit 1
fi
echo "Notarization access OK."
if [[ "${1:-}" == --check ]]; then
    echo "Preflight passed. No packages were signed or submitted."
    exit 0
fi

dotnet run --
echo "Packaging finished successfully."
