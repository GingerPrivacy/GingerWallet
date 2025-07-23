using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.Tiles.PrivacyRing.Interfaces;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Lang;
using DynamicData.Aggregation;

namespace WalletWasabi.Fluent.HomeScreen.Tiles.ViewModels;

public partial class PrivacyControlTileViewModel : ActivatableViewModel, IPrivacyRingPreviewItem
{
	[AutoNotify] private bool _fullyMixed;
	[AutoNotify] private string _percentText = "";

	public PrivacyControlTileViewModel(WalletModel wallet)
	{
		Wallet = wallet;

		IsPrivacyProgressDisplayed = Wallet.Privacy.PrivateAndSemiPrivatePercentage.Select(x => x > 0);
		PrivatePercentage = Wallet.Privacy.PrivatePercentage;
		PrivateAndSemiPrivatePercentage = Wallet.Privacy.PrivateAndSemiPrivatePercentage;
		ProgressText = Wallet.HasBalance.Select(x => x ? Resources.StartCoinjoiningToGainPrivacy : Resources.NoProgressYet);

		ShowDetailsCommand = ReactiveCommand.Create(ShowDetails, Wallet.HasBalance);

		var coinList = Wallet.Coins.List.Connect(suppressEmptyChangeSets: false);

		PrivateAmount = coinList.Filter(x => x.IsPrivate, suppressEmptyChangeSets: false).Sum(set => set.Amount.ToDecimal(MoneyUnit.BTC)).Select(x => wallet.AmountProvider.Create(x));
		HasPrivateBalance = PrivateAmount.Select(x => x.HasBalance);

		SemiPrivateAmount = coinList.Filter(x => x.IsSemiPrivate, suppressEmptyChangeSets: false).Sum(set => set.Amount.ToDecimal(MoneyUnit.BTC)).Select(x => wallet.AmountProvider.Create(x));
		HasSemiPrivateBalance = SemiPrivateAmount.Select(x => x.HasBalance);
	}

	public WalletModel Wallet { get; }

	public IObservable<string> ProgressText { get; set; }

	public IObservable<bool> HasSemiPrivateBalance { get; set; }

	public IObservable<bool> HasPrivateBalance { get; set; }

	public IObservable<Amount> SemiPrivateAmount { get; set; }

	public IObservable<Amount> PrivateAmount { get; set; }

	public IObservable<double> PrivateAndSemiPrivatePercentage { get; set; }

	public IObservable<double> PrivatePercentage { get; set; }

	public IObservable<bool> IsPrivacyProgressDisplayed { get; }

	public ICommand ShowDetailsCommand { get; }

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		var coinList =
			Wallet.Coins.List // coinList here is not subscribed to SmartCoin changes.
				.Connect(suppressEmptyChangeSets: false) // Dynamic updates to SmartCoin properties won't be reflected in the UI.
				.ToCollection(); // See CoinModel.SubscribeToCoinChanges().

		Wallet.Privacy.Progress
			.CombineLatest(Wallet.Privacy.IsWalletPrivate)
			.CombineLatest(coinList)
			.Flatten()
			.Do(tuple =>
			{
				var (privacyProgress, isWalletPrivate, coins) = tuple;
				Update(privacyProgress, isWalletPrivate, coins);
			})
			.Subscribe()
			.DisposeWith(disposables);
	}

	private void ShowDetails()
	{
		UiContext.Navigate().To().PrivacyRing(Wallet);
	}

	private void Update(int privacyProgress, bool isWalletPrivate, IReadOnlyCollection<CoinModel> coins)
	{
		PercentText = privacyProgress.ToString(Resources.Culture);
	}
}
