## How to update Deps.nix file

### Windows (wsl)

1. Run Ubuntu.
2. Install nix (If you have it installed, skip this step)
   ```powershell
   sh <(curl -L https://nixos.org/nix/install) --no-daemon
   ```
2. Go to your `GingerWallet` folder. (The folder where you can find `flake.nix` file)
4. Run the following command to create a bash script:
   ```powershell
   nix build .#packages.x86_64-linux.default.passthru.fetch-deps --extra-experimental-features nix-command --extra-experimental-features flakes
   ```
5. Run the bash script
   ```powershell
   ./result
   ```
6. The last terminal message tells you where is the new nix file. (e.g. `Succesfully wrote lockfile to /tmp/WalletWasabi.Backend-deps-7dpBuk.nix`)
7. Find the file under `\\wsl$\<DistroName>\tmp\`, rename it to `deps.nix` and copy it to GingerWallet folder to override the previous one.