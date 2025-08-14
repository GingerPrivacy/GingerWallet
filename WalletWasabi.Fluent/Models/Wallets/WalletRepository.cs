using DynamicData;
using NBitcoin;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.AddWallet.Models;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Lang;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public class WalletRepository : ReactiveObject
{
	private readonly AmountProvider _amountProvider;
	private readonly HardwareWalletInterface _hardwareWalletInterface;
	private readonly ApplicationSettings _settings;
	private readonly Dictionary<WalletId, WalletModel> _walletDictionary = new();
	private readonly CompositeDisposable _disposable = new();

	public WalletRepository(AmountProvider amountProvider, HardwareWalletInterface hardwareWalletInterface, ApplicationSettings settings)
	{
		_amountProvider = amountProvider;
		_hardwareWalletInterface = hardwareWalletInterface;
		_settings = settings;

		var walletAdded = Observable.FromEventPattern<Wallet>(Services.WalletManager, nameof(WalletManager.WalletAdded)).ToSignal();
		var walletRemoved = Observable.FromEventPattern<Wallet>(Services.WalletManager, nameof(WalletManager.WalletRemoved)).ToSignal();
		var signals = walletAdded.Merge(walletRemoved).StartWith(System.Reactive.Unit.Default);

		Wallets =
			signals.Fetch(() => Services.WalletManager.GetWallets(), x => x.WalletId)
				.DisposeWith(_disposable)
				.Connect()
				.TransformWithInlineUpdate(CreateWalletModel, (_, _) => { })
				.DisposeMany()
				.AsObservableCache()
				.DisposeWith(_disposable);

		DefaultWalletName = _settings.LastSelectedWallet;
	}

	public IObservableCache<WalletModel, WalletId> Wallets { get; }

	public string? DefaultWalletName { get; }
	public bool HasWallet => Services.WalletManager.HasWallet();

	private KeyPath AccountKeyPath { get; } = KeyManager.GetAccountKeyPath(Services.WalletManager.Network, ScriptPubKeyType.Segwit);

	public void StoreLastSelectedWallet(WalletModel wallet)
	{
		_settings.LastSelectedWallet = wallet.Name;
	}

	public string GetNextWalletName()
	{
		return Services.WalletManager.WalletDirectories.GetNextWalletName(Resources.Wallet);
	}

	public async Task<WalletSettingsModel> NewWalletAsync(WalletCreationOptions options, CancellationToken? cancelToken = null)
	{
		return options switch
		{
			WalletCreationOptions.AddNewWallet add => await CreateNewWalletAsync(add),
			WalletCreationOptions.ConnectToHardwareWallet hw => await ConnectToHardwareWalletAsync(hw, cancelToken),
			WalletCreationOptions.ImportWallet import => await ImportWalletAsync(import),
			WalletCreationOptions.RecoverWallet recover => await RecoverWalletAsync(recover),
			_ => throw new InvalidOperationException($"{nameof(WalletCreationOptions)} not supported: {options?.GetType().Name}")
		};
	}

	public bool RemoveWallet(WalletModel wallet)
	{
		return Services.WalletManager.RemoveWallet(wallet.Wallet);
	}

	public WalletModel SaveWallet(WalletSettingsModel walletSettings)
	{
		var id = walletSettings.Save();
		var result = GetById(id);
		result.Settings.IsCoinJoinPaused = walletSettings.IsCoinJoinPaused;
		return result;
	}

	public (ErrorSeverity Severity, string Message)? ValidateWalletName(string walletName)
	{
		return Services.WalletManager.ValidateWalletName(walletName);
	}

	public void LoadWalletListFromFileSystem()
	{
		Services.WalletManager.LoadWalletListFromFileSystem();
	}

	public WalletModel? GetExistingWallet(HwiEnumerateEntry device)
	{
		var existingWallet = Services.WalletManager.GetWallets().FirstOrDefault(x => x.KeyManager.MasterFingerprint == device.Fingerprint);
		if (existingWallet is { })
		{
			return GetById(existingWallet.WalletId);
		}

		return null;
	}

	private async Task<WalletSettingsModel> CreateNewWalletAsync(WalletCreationOptions.AddNewWallet options)
	{
		var (walletName, password, mnemonic) = options;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentNullException.ThrowIfNull(password);

		var (keyManager, _) = await Task.Run(() =>
		{
			var walletGenerator = new WalletGenerator(
				Services.WalletManager.WalletDirectories.WalletsDir,
				Services.WalletManager.Network)
			{
				TipHeight = Services.SmartHeaderChain.TipHeight
			};
			return walletGenerator.GenerateWallet(walletName, password, mnemonic);
		});

		return new WalletSettingsModel(keyManager, true);
	}

	private async Task<WalletSettingsModel> ConnectToHardwareWalletAsync(WalletCreationOptions.ConnectToHardwareWallet options, CancellationToken? cancelToken)
	{
		var (walletName, device) = options;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentNullException.ThrowIfNull(device);
		ArgumentNullException.ThrowIfNull(cancelToken);

		var walletFilePath = Services.WalletManager.WalletDirectories.GetWalletFilePaths(walletName).walletFilePath;
		var keyManager = await _hardwareWalletInterface.GenerateWalletAsync(device, walletFilePath, cancelToken.Value);
		keyManager.SetIcon(device.WalletType);
		keyManager.Attributes.IsRecovering = true;

		var result = new WalletSettingsModel(keyManager, true);
		return result;
	}

	private async Task<WalletSettingsModel> ImportWalletAsync(WalletCreationOptions.ImportWallet options)
	{
		var (walletName, filePath) = options;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentException.ThrowIfNullOrEmpty(filePath);

		var keyManager = await ImportWalletHelper.ImportWalletAsync(Services.WalletManager, walletName, filePath);
		return new WalletSettingsModel(keyManager, true);
	}

	private async Task<WalletSettingsModel> RecoverWalletAsync(WalletCreationOptions.RecoverWallet options)
	{
		var (walletName, password, mnemonic, minGapLimit) = options;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentNullException.ThrowIfNull(password);
		ArgumentNullException.ThrowIfNull(mnemonic);
		ArgumentNullException.ThrowIfNull(minGapLimit);

		var keyManager = await Task.Run(() =>
		{
			var walletFilePath = Services.WalletManager.WalletDirectories.GetWalletFilePaths(walletName).walletFilePath;

			var result = KeyManager.Recover(
				mnemonic,
				password,
				Services.WalletManager.Network,
				AccountKeyPath,
				null,
				"", // Make sure it is not saved into a file yet.
				minGapLimit.Value);

			result.AutoCoinJoin = true;
			result.Attributes.IsRecovering = true;

			// Set the filepath but we will only write the file later when the Ui workflow is done.
			result.SetFilePath(walletFilePath);

			return result;
		});

		return new WalletSettingsModel(keyManager, true, true);
	}

	private WalletModel GetById(WalletId id)
	{
		return
			_walletDictionary.TryGetValue(id, out var wallet)
				? wallet
				: throw new InvalidOperationException($"Wallet not found: {id}");
	}

	private WalletModel CreateWalletModel(Wallet wallet)
	{
		if (_walletDictionary.TryGetValue(wallet.WalletId, out var existing))
		{
			if (!ReferenceEquals(existing.Wallet, wallet))
			{
				throw new InvalidOperationException($"Different instance of: {wallet.WalletName}");
			}

			return existing;
		}

		var result =
			wallet.KeyManager.IsHardwareWallet
				? new HardwareWalletModel(wallet, _amountProvider)
				: new WalletModel(wallet, _amountProvider);

		_walletDictionary[wallet.WalletId] = result;

		return result;
	}
}
