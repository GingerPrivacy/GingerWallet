using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.CoinjoinPlayer.ViewModel;
using WalletWasabi.Fluent.HomeScreen.History.ViewModels;
using WalletWasabi.Fluent.HomeScreen.Tiles.ViewModels;
using WalletWasabi.Fluent.HomeScreen.WalletSettings.ViewModels;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.Models;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Fluent.SearchBar.Interfaces;
using WalletWasabi.Fluent.SearchBar.ViewModels.SearchItems;
using WalletWasabi.Fluent.SearchBar.ViewModels.Sources;
using WalletWasabi.Lang;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.Wallets.ViewModels;

[AppLifetime]
[NavigationMetaData(NavigationTarget = NavigationTarget.HomeScreen)]
public partial class WalletViewModel : RoutableViewModel
{
	[AutoNotify(SetterModifier = AccessModifier.Protected)] private bool _isCoinJoining;

	[AutoNotify(SetterModifier = AccessModifier.Protected)] private bool _isLoading;
	[AutoNotify] private bool _isPointerOver;
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private bool _hasBuyOrderOnHold;
	[AutoNotify] private bool _hasSellOrderOnHold;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isSendButtonVisible;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isWalletBalanceZero;
	[AutoNotify(SetterModifier = AccessModifier.Protected)] private WalletState _walletState;

	public WalletViewModel(WalletModel walletModel, Wallet wallet)
	{
		WalletModel = walletModel;
		Wallet = wallet;

		Settings = new WalletSettingsViewModel(WalletModel);
		History = new HistoryViewModel(WalletModel);
		var searchItems = CreateSearchItems();
		this.WhenAnyValue(x => x.IsSelected)
			.Do(shouldDisplay => UiContext.EditableSearchSource.Toggle(searchItems, shouldDisplay))
			.Subscribe();

		var sendSearchItem = CreateSendItem();
		this.WhenAnyValue(x => x.IsSendButtonVisible, x => x.IsSelected, (x, y) => x && y)
			.Do(shouldAdd => UiContext.EditableSearchSource.Toggle(sendSearchItem, shouldAdd))
			.Subscribe();

		walletModel.HasBalance
			.Select(x => !x)
			.BindTo(this, x => x.IsWalletBalanceZero);

		walletModel.Coinjoin.IsRunning
			.BindTo(this, x => x.IsCoinJoining);

		this.WhenAnyValue(x => x.IsWalletBalanceZero)
			.Subscribe(_ => IsSendButtonVisible = !IsWalletBalanceZero && (!WalletModel.IsWatchOnlyWallet || WalletModel.IsHardwareWallet));

		IsMusicBoxVisible =
			this.WhenAnyValue(x => x.IsSelected, x => x.IsWalletBalanceZero, x => x.CoinjoinPlayerViewModel.AreAllCoinsPrivate, x => x.IsPointerOver)
				.Throttle(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
				.Select(tuple =>
				{
					var (isSelected, isWalletBalanceZero, areAllCoinsPrivate, pointerOver) = tuple;
					return (isSelected && !isWalletBalanceZero && (!areAllCoinsPrivate || pointerOver)) && !WalletModel.IsWatchOnlyWallet;
				});

		BuyCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().Buy(walletModel));
		SellCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().Sell(walletModel));

		SendCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().Send(walletModel, new SendFlowModel(wallet, walletModel)));
		SendManualControlCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().ManualControlDialog(walletModel, wallet));

		ReceiveCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().Receive(WalletModel));

		WalletInfoCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (await AuthorizeForPasswordAsync())
			{
				UiContext.Navigate().To().WalletInfo(WalletModel);
			}
		});

		WalletStatsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().WalletStats(WalletModel));

		WalletSettingsCommand = ReactiveCommand.Create(
			() =>
			{
				Settings.SelectedTab = 0;
				UiContext.Navigate().Navigate(Settings.DefaultTarget).To(Settings);
			});

		CoinJoinSettingsCommand = ReactiveCommand.Create(
			() =>
			{
				Settings.SelectedTab = 1;
				UiContext.Navigate().Navigate(Settings.DefaultTarget).To(Settings);
			});

		WalletCoinsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().WalletCoins(WalletModel));

		CoinjoinPlayerViewModel = new CoinjoinPlayerViewModel(WalletModel, Settings);

		Tiles = GetTiles().ToList();

		this.WhenAnyValue(x => x.Settings.PreferPsbtWorkflow)
			.Do(x => this.RaisePropertyChanged(nameof(PreferPsbtWorkflow)))
			.Subscribe();

		this.WhenAnyValue(x => x.WalletModel.Name).BindTo(this, x => x.Title);
	}

	// TODO: Remove this
	public Wallet Wallet { get; }

	public WalletModel WalletModel { get; }

	public bool IsLoggedIn => WalletModel.Auth.IsLoggedIn;

	public bool PreferPsbtWorkflow => WalletModel.Settings.PreferPsbtWorkflow;

	public bool IsWatchOnly => WalletModel.IsWatchOnlyWallet;

	public IObservable<bool> IsMusicBoxVisible { get; }

	public CoinjoinPlayerViewModel CoinjoinPlayerViewModel { get; private set; }

	public WalletSettingsViewModel Settings { get; private set; }

	public HistoryViewModel History { get; }

	public IEnumerable<ActivatableViewModel> Tiles { get; }

	public ICommand BuyCommand { get; private set; }

	public ICommand SellCommand { get; private set; }

	public ICommand SendCommand { get; private set; }

	public ICommand SendManualControlCommand { get; }

	public ICommand? BroadcastPsbtCommand { get; set; }

	public ICommand ReceiveCommand { get; private set; }

	public ICommand WalletInfoCommand { get; private set; }

	public ICommand WalletSettingsCommand { get; private set; }

	public ICommand WalletStatsCommand { get; private set; }

	public ICommand WalletCoinsCommand { get; private set; }

	public ICommand CoinJoinSettingsCommand { get; private set; }

	public void SelectTransaction(uint256 txid)
	{
		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			await Task.Delay(500);
			History.SelectTransaction(txid);
		});
	}

	public void NavigateAndHighlight(uint256 txid)
	{
		UiContext.Navigate(DefaultTarget).To(this, NavigationMode.Clear);

		SelectTransaction(txid);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		History.Activate(disposables);

		foreach (var tile in Tiles)
		{
			tile.Activate(disposables);
		}

		this.WhenAnyObservable(x => x.WalletModel.BuySellModel.HasBuyOrderOnHold)
			.BindTo(this, x => x.HasBuyOrderOnHold)
			.DisposeWith(disposables);

		this.WhenAnyObservable(x => x.WalletModel.BuySellModel.HasSellOrderOnHold)
			.BindTo(this, x => x.HasSellOrderOnHold)
			.DisposeWith(disposables);

		WalletModel.State
			.BindTo(this, x => x.WalletState)
			.DisposeWith(disposables);

		if (!IsWatchOnly && !Settings.WalletCoinJoinSettings.IsCoinjoinProfileSelected)
		{
			UiContext.Navigate().To().ConfirmCoinjoinSettings(Settings);
		}
	}

	private ISearchItem[] CreateSearchItems()
	{
		return new ISearchItem[]
		{
			new ActionableItem(Resources.ReceiveViewModelTitle, Resources.ReceiveViewModelCaption, () => { ReceiveCommand.ExecuteIfCan(); return Task.CompletedTask; }, Resources.Wallet, Resources.ReceiveViewModelKeywords.ToKeywords()) { Icon = "wallet_action_receive", IsDefault = true, Priority = 2 },
			new ActionableItem(Resources.CoinjoinSettings, Resources.WalletCoinJoinSettingsViewModelCaption, () => { CoinJoinSettingsCommand.ExecuteIfCan(); return Task.CompletedTask; }, Resources.Wallet, Resources.WalletSettingsKeywords.ToKeywords()) { Icon = "wallet_action_coinjoin", IsDefault = true, Priority = 3 },
			new ActionableItem(Resources.WalletSettings, Resources.WalletSettingsViewModelCaption, () => { WalletSettingsCommand.ExecuteIfCan(); return Task.CompletedTask; }, Resources.Wallet, Resources.WalletSettingsKeywords.ToKeywords()) { Icon = "settings_wallet_regular", IsDefault = true, Priority = 4 },
			new ActionableItem(Resources.ExcludedCoinsViewModelTitle, Resources.ExcludedCoinsViewModelCaption, () => { CoinjoinPlayerViewModel.NavigateToExcludedCoinsCommand.ExecuteIfCan(); return Task.CompletedTask; }, Resources.Wallet, Resources.ExcludedCoinsViewModelKeywords.ToKeywords()) { Icon = "exclude_coins", IsDefault = true, Priority = 5 },
			new ActionableItem(Resources.WalletCoinsViewModelTitle, Resources.WalletCoinsViewModelCaption, () => { WalletCoinsCommand.ExecuteIfCan(); return Task.CompletedTask; }, Resources.Wallet, Resources.WalletCoinsViewModelKeywords.ToKeywords()) { Icon = "wallet_coins", IsDefault = true, Priority = 6 },
			new ActionableItem(Resources.WalletStatsViewModelTitle, Resources.WalletStatsViewModelCaption, () => { WalletStatsCommand.ExecuteIfCan(); return Task.CompletedTask; }, Resources.Wallet, Resources.WalletStatsViewModelKeywords.ToKeywords()) { Icon = "stats_wallet_regular", IsDefault = true, Priority = 7 },
			new ActionableItem(Resources.WalletInfoViewModelTitle, Resources.WalletInfoViewModelCaption, () => { WalletInfoCommand.ExecuteIfCan(); return Task.CompletedTask; }, Resources.Wallet, Resources.WalletInfoViewModelKeywords.ToKeywords()) { Icon = "info_regular", IsDefault = true, Priority = 8 },
		};
	}

	private ISearchItem CreateSendItem()
	{
		return new ActionableItem(Resources.SendViewModelTitle, Resources.SendViewModelCaption, () => { SendCommand.ExecuteIfCan(); return Task.CompletedTask; }, Resources.Wallet, Resources.AboutViewModelKeywords.ToKeywords()) { Icon = "wallet_action_send", IsDefault = true, Priority = 1 };
	}

	private IEnumerable<ActivatableViewModel> GetTiles()
	{
		yield return new WalletBalanceTileViewModel(WalletModel.Balances);

		if (!IsWatchOnly)
		{
			yield return new PrivacyControlTileViewModel(WalletModel);
		}

		yield return new BtcPriceTileViewModel(UiContext.AmountProvider);
	}

	private async Task<bool> AuthorizeForPasswordAsync()
	{
		if (WalletModel.Auth.HasPassword)
		{
			return await UiContext.Navigate().To().PasswordAuthDialog(WalletModel).GetResultAsync();
		}

		return true;
	}
}
