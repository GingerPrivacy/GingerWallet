using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;

namespace WalletWasabi.Fluent.HomeScreen.Others.ViewModels;

[NavigationMetaData(
	IconName = "nav_wallet_24_regular",
	Order = 3,
	Category = SearchCategory.Wallet,
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false,
	IsLocalized = true)]
public partial class WalletStatsViewModel : RoutableViewModel
{
	private readonly WalletModel _wallet;
	[AutoNotify] private WalletStatsModel? _model;

	public WalletStatsViewModel(WalletModel wallet)
	{
		_wallet = wallet;

		NextCommand = ReactiveCommand.Create(() => UiContext.Navigate(CurrentTarget).Clear());
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		Model = _wallet.GetWalletStats().DisposeWith(disposables);
	}
}
