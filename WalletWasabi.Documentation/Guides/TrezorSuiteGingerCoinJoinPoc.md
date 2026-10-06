# Testing the Trezor Suite Ginger CoinJoin PoC

> [!WARNING]
> This pull request is an experimental proof of concept, not a supported release feature. It changes an internal Trezor Suite debug setting and has received only limited real-hardware testing. One low-value round completed, but repeated-round behavior remains under investigation. Use only an empty or low-value test account. Never test with funds you cannot afford to lose.

## What this PoC tests

The PoC adds a **Settings > Trezor** tab to Ginger Wallet. It:

1. Finds the installed Trezor Suite executable on Windows, macOS, or Linux, with a manual file picker as a fallback.
2. Checks that the Ginger coordinator and backend are reachable.
3. Starts Trezor Suite briefly with a random loopback Chrome DevTools Protocol port.
4. Backs up Suite's existing `coinjoinDebugSettings/debug` IndexedDB value.
5. Preserves unrelated debug fields and applies these Bitcoin overrides:
   - Coordinator: `https://api.gingerwallet.io/WabiSabi/`
   - WabiSabi backend: `https://api.gingerwallet.io/`
   - Affiliation ID: `null`
6. Reads the value back and marks it configured only if all three values match. It also backs up and applies the selected anonymity target to remembered Bitcoin CoinJoin accounts with Custom settings, verifying each write.
7. Closes the temporary Suite process and opens Suite with diagnostic file logging enabled.

Ginger does not monitor Suite after launch and does not access the Trezor seed, PIN, passphrase, or private keys.

## Prerequisites

- A supported desktop operating system: Windows, macOS, or Linux.
- The .NET SDK version selected by this repository's `global.json`.
- A recent Trezor Suite desktop installation.
- A Trezor hardware wallet only for the optional hardware test.
- Trezor Suite fully closed before every **Configure**, **Repair**, or **Restore** operation.

Record the operating system, Ginger commit, Trezor Suite version, device model, and firmware version for the test report. Do not include wallet identifiers or transaction details.

## Build and run the PR

Check out the pull request using either GitHub CLI:

```shell
gh pr checkout 196
```

Or fetch it with Git:

```shell
git fetch https://github.com/GingerPrivacy/GingerWallet.git pull/196/head:trezor-suite-ginger-poc
git switch trezor-suite-ginger-poc
```

Restore, build, and run Ginger:

```shell
dotnet restore --locked-mode
dotnet build WalletWasabi.Fluent.Desktop/WalletWasabi.Fluent.Desktop.csproj --configuration Debug
dotnet run --project WalletWasabi.Fluent.Desktop/WalletWasabi.Fluent.Desktop.csproj --configuration Debug
```

## Test 1: detection and configuration

1. Close every Trezor Suite window and confirm Suite is no longer running.
2. In Ginger, open **Settings > Trezor**.
3. Confirm the tab shows:
   - **Trezor Suite:** `Installed`
   - A plausible **Trezor Suite version**
   - **Ginger CoinJoin backend:** `Online`
   - **Configuration:** `Not configured`
   - **Last verified:** `Never`
4. If Suite is not detected, click **Choose** and select its executable or app binary.
5. Click **Configure and open Trezor Suite**.
6. A temporary Suite/DevTools window may appear briefly. Ginger should close that process and open Suite normally.
7. Return to Ginger and press **Refresh** if necessary.
8. Confirm:
   - **Configuration:** `Configured for Ginger`
   - **Last verified:** contains the time of this test
   - The primary button is now **Repair and open Trezor Suite**
   - **Restore original configuration** is available
   - **Trezor Suite diagnostic logs** shows a path and **Open folder** opens it

`Installed` and `Online` only confirm prerequisites. The override is successful only when **Configuration** says `Configured for Ginger` and **Last verified** has a timestamp.

## Test 2: failure handling

### Anonymity target

The **Suite anonymity target** box defaults to **3** and accepts whole numbers from **2 to 100**. The value is saved in Ginger when you configure/repair Suite. This is a low-privacy experimental default, not a privacy guarantee. Lowering it changes the Private classification, not the actual anonymity of existing coins.

1. In Suite, create and remember the test Bitcoin CoinJoin account. Under **Details**, select **Custom** and check the fee limit and round-skipping setting. Close Suite completely.
2. In Ginger, enter the desired target and click **Configure / Repair and open Trezor Suite**. This applies to **all remembered Bitcoin CoinJoin accounts** in that Suite profile, not testnet accounts.
3. Verify the target in Suite's **Details** tab. The existing fee limit and round-skipping setting must be unchanged.
4. Confirm Ginger reports the number of saved accounts updated. With no saved accounts it explicitly reports zero: create/remember the account, select Custom and configure again. Newly created accounts do not inherit Ginger's value automatically.
5. Try an empty, fractional, nonnumeric, or out-of-range value. Ginger must refuse it before launching Suite. Recommended or incompatible saved settings must produce a clear error rather than invented fee settings.
6. Change the target in Suite, close it, and use **Open Trezor Suite** from Ginger: it must not overwrite that change. **Configure / Repair** explicitly reapplies the value in Ginger's box.
7. **Restore original configuration** restores the first backed-up target for each modified account while preserving its other current settings. Deleted accounts are not recreated. Original backups made by older PoC builds remain supported.

The integration backup now also contains Suite account identifiers and original target values. Treat it as private wallet metadata; do not publish it.

### Running Suite / configuration failures

1. Leave Trezor Suite running.
2. Click **Repair and open Trezor Suite** in Ginger.
3. Confirm Ginger reports the operation as failed rather than claiming it was verified.
4. Close Suite completely and repeat the operation.
5. Confirm the repair then succeeds and Suite opens normally.

Do not terminate Ginger or Suite while Ginger is applying or restoring the setting.

## Test 3: Suite CoinJoin behavior

The presence and reachability of CoinJoin controls can differ between Trezor Suite versions. Automatic continuation into later rounds is still under investigation.

1. Connect the Trezor device and open a Bitcoin account in Suite.
2. Record whether Suite exposes any CoinJoin account or CoinJoin action after the override.
3. If no CoinJoin action appears, stop here and report the result with the Suite version.
4. If CoinJoin is available, use only a fresh, low-value test account.
5. Confirm that Suite can load coordinator round information without an affiliation error.
6. If you deliberately proceed into a round, verify every amount and destination on the hardware display. Abort on any unexpected prompt.
7. Report the furthest successfully completed protocol phase. Do not publish addresses, transaction IDs, xpubs, screenshots containing balances, or full logs.

Successful configuration proves only that Suite accepted the backend override. It does not prove that the current Suite UI exposes CoinJoin or that a hardware-signed round completes.

### Collect diagnostic logs

Every Trezor Suite process started by the Ginger Trezor tab uses Suite's `--log-write` and `--log-level=debug` switches. Logs are written to the `TrezorSuiteIntegration/SuiteLogs` directory below Ginger's platform-specific data directory. Use **Settings > Trezor > Open folder** to locate them. Coin-selection messages such as `Found account candidate`, `Utxos 0`, `detained`, or `Too many unavailable utxos` can help distinguish stale account data from a temporarily blocked input.

When reporting a failure, close Suite first so that it flushes the current log, then copy only the relevant nearby `@trezor/coinjoin`, `CoinjoinClient`, or `CoinjoinBackend` lines. Logs may contain wallet metadata. Remove usernames, filesystem paths, wallet identifiers, addresses, transaction IDs, xpubs, balances, and any other sensitive details before sharing. Never post an entire raw log.

## Test 4: restore the original Suite setting

1. Close Trezor Suite completely.
2. In **Settings > Trezor**, click **Restore original configuration**.
3. Ginger should restore the exact previous debug value. If no value existed before the test, it deletes the value created by the PoC.
4. Confirm Suite opens normally.
5. Return to Ginger and confirm the configuration is no longer marked as configured and the Restore button is gone.

Do this even if the functional CoinJoin test was unsuccessful.

## Reporting results

Use this sanitized template in the pull request:

```text
OS:
Ginger commit:
Trezor Suite version:
Trezor model and firmware:

Suite detected automatically: yes/no
Override verified by Ginger: yes/no
CoinJoin controls visible in Suite: yes/no
Round information loaded: yes/no/not tested
Hardware signing reached: yes/no/not tested
Original setting restored: yes/no

Error shown by Ginger, if any:
Additional sanitized observations:
```

If a failure occurs, include the concise Ginger error and the relevant nearby log lines. Remove paths containing personal usernames and all wallet-specific data before posting.

## Known PoC limitations

- Trezor Suite must already be fully closed; Ginger does not terminate an independently running Suite instance.
- Compatibility is version-dependent because this uses Suite's internal debug storage.
- The loopback debugging interface exists only during configuration or restoration, but another local process could theoretically connect to it.
- Backend reachability and the last successful write are checked; there is no continuous Suite monitoring.
- Diagnostic logs accumulate until the tester removes them from the folder shown by Ginger.
- One low-value mainnet hardware CoinJoin has completed; reliable continuation into subsequent rounds has not yet been established.
