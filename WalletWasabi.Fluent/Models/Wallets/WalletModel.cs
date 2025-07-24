using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.HomeScreen.BuySell.Models;
using WalletWasabi.Fluent.HomeScreen.Labels.Models;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletModel : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _disposable = new();

	private readonly Lazy<WalletCoinjoinModel> _coinjoin;
	private readonly Lazy<WalletCoinsModel> _coins;
	private readonly Lazy<BuySellModel> _buySellModel;

	[AutoNotify] private bool _isLoggedIn;
	[AutoNotify] private bool _isLoaded;
	[AutoNotify] private bool _isSelected;

	public WalletModel(Wallet wallet, AmountProvider amountProvider)
	{
		Wallet = wallet;
		AmountProvider = amountProvider;

		Auth = new WalletAuthModel(this, Wallet);
		Loader = new WalletLoadWorkflow(Wallet);
		Settings = new WalletSettingsModel(Wallet.KeyManager);

		_coinjoin = new(() => new WalletCoinjoinModel(Wallet, Settings));
		_coins = new(() => new WalletCoinsModel(wallet, this));
		_buySellModel = new(() => new BuySellModel(wallet));

		Transactions = new WalletTransactionsModel(this, wallet);

		Addresses = new AddressesModel(Wallet);

		State =
			Observable.FromEventPattern<WalletState>(Wallet, nameof(Wallet.StateChanged))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .Select(_ => Wallet.State);

		Privacy = new WalletPrivacyModel(this, Wallet);

		Balances = Transactions.TransactionProcessed
			.Select(_ => Wallet.Coins.TotalAmount())
			.Select(AmountProvider.Create);

		HasBalance = Balances.Select(x => x.HasBalance);

		// Start the Loader after wallet is logged in
		this.WhenAnyValue(x => x.Auth.IsLoggedIn)
			.Where(x => x)
			.Take(1)
			.Do(_ => Loader.Start())
			.Subscribe();

		// Stop the loader after load is completed
		State.Where(x => x == WalletState.Started)
			 .Do(_ => Loader.Stop())
			 .Subscribe();

		this.WhenAnyValue(x => x.Auth.IsLoggedIn)
			.BindTo(this, x => x.IsLoggedIn)
			.DisposeWith(_disposable);

		this.WhenAnyObservable(x => x.State)
			.Select(x => x == WalletState.Started)
			.BindTo(this, x => x.IsLoaded)
			.DisposeWith(_disposable);

		this.WhenAnyValue(x => x.IsLoaded)
			.Where(x => x)
			.Take(1)
			.Subscribe(_ =>
			{
				Settings.IsRecovering = false;
				Settings.Save();
			});
	}

	public AddressesModel Addresses { get; }

	// TODO: Make this internal after Send refactoring
	public Wallet Wallet { get; }

	public WalletId Id => Wallet.WalletId;

	public string Name => Wallet.WalletName;

	public Network Network => Wallet.Network;

	public WalletTransactionsModel Transactions { get; }

	public IObservable<Amount> Balances { get; }

	public IObservable<bool> HasBalance { get; }

	public WalletAuthModel Auth { get; }

	public WalletLoadWorkflow Loader { get; }

	public WalletSettingsModel Settings { get; }

	public WalletPrivacyModel Privacy { get; }

	public IObservable<WalletState> State { get; }

	public AmountProvider AmountProvider { get; }

	public WalletCoinjoinModel Coinjoin => _coinjoin.Value;

	public WalletCoinsModel Coins => _coins.Value;

	public BuySellModel BuySellModel => _buySellModel.Value; // TODO: Source gen is idiot, investigate what's its problem if interfaces is used.

	public bool IsHardwareWallet => Wallet.KeyManager.IsHardwareWallet;

	public bool IsWatchOnlyWallet => Wallet.KeyManager.IsWatchOnly;

	public bool IsTaprootSupported => Wallet.KeyManager.TaprootExtPubKey is not null;

	public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
	{
		return Wallet.GetLabelsWithRanking(intent);
	}

	public WalletStatsModel GetWalletStats()
	{
		return new WalletStatsModel(this, Wallet);
	}

	public WalletInfoModel GetWalletInfo()
	{
		return new WalletInfoModel(Wallet);
	}

	public PrivacySuggestionsModel GetPrivacySuggestionsModel(SendFlowModel sendFlow)
	{
		return new PrivacySuggestionsModel(sendFlow);
	}

	public void Rename(string newWalletName)
	{
		Services.WalletManager.RenameWallet(Wallet, newWalletName);
		this.RaisePropertyChanged(nameof(Name));
	}

	public void Dispose()
	{
		DisposeIfCreated(_coinjoin);
		DisposeIfCreated(_coins);
		DisposeIfCreated(_buySellModel);
		Transactions.Dispose();
		Addresses.Dispose();
		_disposable.Dispose();

		return;

		void DisposeIfCreated<T>(Lazy<T> objectToDispose)
		{
			if (objectToDispose is { IsValueCreated: true, Value: IDisposable disposable })
			{
				disposable.Dispose();
			}
		}
	}
}
