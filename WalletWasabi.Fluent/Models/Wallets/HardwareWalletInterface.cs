using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Models.Wallets;

public class HardwareWalletInterface
{
	private readonly Network _network;

	public HardwareWalletInterface(Network network)
	{
		_network = network;
	}

	public async Task<HwiEnumerateEntry[]> DetectAsync(CancellationToken cancelToken)
	{
		var client = new HwiClient(_network);
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancelToken);

		var detectedHardwareWallets = (await client.EnumerateAsync(timeoutCts.Token).ConfigureAwait(false)).ToArray();

		cancelToken.ThrowIfCancellationRequested();

		return detectedHardwareWallets;
	}

	public async Task InitHardwareWalletAsync(HwiEnumerateEntry device, CancellationToken cancelToken)
	{
		var client = new HwiClient(_network);
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(21));
		using var initCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancelToken);

		// Trezor T doesn't require interactive mode.
		var interactiveMode = !(device.Model == HardwareWalletModels.Trezor_T || device.Model == HardwareWalletModels.Trezor_T_Simulator);

		try
		{
			await client.SetupAsync(device.Model, device.Path, interactiveMode, initCts.Token).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	public async Task<KeyManager> GenerateWalletAsync(HwiEnumerateEntry device, string walletFilePath, CancellationToken cancelToken)
	{
		if (device.Fingerprint is null)
		{
			throw new Exception("Fingerprint cannot be null.");
		}

		var client = new HwiClient(_network);
		var fingerPrint = (HDFingerprint)device.Fingerprint;

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		using var genCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancelToken);

		var segwitExtPubKey = await client.GetXpubAsync(
			device.Model,
			device.Path,
			KeyManager.GetAccountKeyPath(_network, ScriptPubKeyType.Segwit),
			genCts.Token).ConfigureAwait(false);

		return KeyManager.CreateNewHardwareWalletWatchOnly(fingerPrint, segwitExtPubKey, null, _network, walletFilePath);
	}
}
