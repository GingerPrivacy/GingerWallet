using System.Collections.Generic;
using System.Reactive.Disposables;
using WalletWasabi.Fluent.CoinList.ViewModels;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;

namespace WalletWasabi.Fluent.HomeScreen.Others.ViewModels;

[NavigationMetaData(
	IconName = "nav_wallet_24_regular",
	Order = 0,
	Category = SearchCategory.Wallet,
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false,
	IsLocalized = true)]
public partial class WalletCoinsViewModel : RoutableViewModel
{
	private readonly WalletModel _wallet;

	public WalletCoinsViewModel(WalletModel wallet)
	{
		_wallet = wallet;
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = CancelCommand;
		CoinList = new CoinListViewModel(_wallet.Coins, new List<CoinModel>(), allowCoinjoiningCoinSelection: false, ignorePrivacyMode: false, allowSelection: false);
	}

	public CoinListViewModel CoinList { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		if (!isInHistory)
		{
			CoinList.ExpandAllCommand.Execute().Subscribe().DisposeWith(disposables);
		}
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		if (!isInHistory)
		{
			CoinList.Dispose();
		}
	}
}
