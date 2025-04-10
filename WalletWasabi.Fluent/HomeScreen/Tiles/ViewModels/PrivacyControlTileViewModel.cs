using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
	private readonly IWalletModel _wallet;
	[AutoNotify] private bool _fullyMixed;
	[AutoNotify] private string _percentText = "";

	public PrivacyControlTileViewModel(IWalletModel wallet)
	{
		_wallet = wallet;

		IsPrivacyProgressDisplayed = wallet.Privacy.PrivateAndSemiPrivatePercentage.Select(x => x > 0);
		PrivatePercentage = wallet.Privacy.PrivatePercentage;
		PrivateAndSemiPrivatePercentage = wallet.Privacy.PrivateAndSemiPrivatePercentage;
		ProgressText = wallet.HasBalance.Select(x => x ? Resources.StartCoinjoiningToGainPrivacy : Resources.NoProgressYet);

		ShowDetailsCommand = ReactiveCommand.Create(ShowDetails, _wallet.HasBalance);

		var coinList = _wallet.Coins.List.Connect(suppressEmptyChangeSets: false);

		PrivateAmount = coinList.Filter(x => x.IsPrivate, suppressEmptyChangeSets: false).Sum(set => set.Amount.ToDecimal(MoneyUnit.BTC)).Select(x => wallet.AmountProvider.Create(x));
		HasPrivateBalance = PrivateAmount.Select(x => x.HasBalance);

		SemiPrivateAmount = coinList.Filter(x => x.IsSemiPrivate, suppressEmptyChangeSets: false).Sum(set => set.Amount.ToDecimal(MoneyUnit.BTC)).Select(x => wallet.AmountProvider.Create(x));
		HasSemiPrivateBalance = SemiPrivateAmount.Select(x => x.HasBalance);
	}

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
			_wallet.Coins.List                                      // coinList here is not subscribed to SmartCoin changes.
						 .Connect(suppressEmptyChangeSets: false)   // Dynamic updates to SmartCoin properties won't be reflected in the UI.
						 .ToCollection();                           // See CoinModel.SubscribeToCoinChanges().

		_wallet.Privacy.Progress
					   .CombineLatest(_wallet.Privacy.IsWalletPrivate)
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
		UiContext.Navigate().To().PrivacyRing(_wallet);
	}

	private void Update(int privacyProgress, bool isWalletPrivate, IReadOnlyCollection<ICoinModel> coins)
	{
		PercentText = privacyProgress.ToString(Resources.Culture);
	}
}
