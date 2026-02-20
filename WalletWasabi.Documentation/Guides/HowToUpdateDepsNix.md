## How to update `deps.nix`

### Windows (WSL)

1. Open **Ubuntu (WSL)**.
2. Install **Nix** (skip if already installed):
   ```bash
   sh <(curl -L https://nixos.org/nix/install) --no-daemon
   ```
3. Go to the repository folder (anywhere inside the repo is fine, the script finds `flake.nix` automatically):
   ```bash
   cd /mnt/c/Users/<You>/Documents/work/GingerPrivacy/GingerBackend
   ```
4. Run the update script:
   ```bash
   ./Contrib/update-nuget-deps.sh
   ```
5. Commit the updated lockfile:
   ```bash
   git add deps.nix
   git commit -m "Update deps.nix"
   ```
