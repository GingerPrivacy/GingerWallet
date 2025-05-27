using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.NavBar.ViewModels;

namespace WalletWasabi.Fluent.HomeScreen.Notifications.ViewModels;

[AppLifetime]
public partial class WalletNotificationsViewModel : ViewModelBase
{
	private readonly NavBarViewModel _navBar;
	[AutoNotify] private bool _isBusy;

	public WalletNotificationsViewModel(NavBarViewModel navBar)
	{
		_navBar = navBar;
	}

	public void StartListening()
	{
		UiContext.WalletRepository.Wallets
			.Connect()
			.AutoRefresh(x => x.IsLoggedIn)
			.Filter(x => x.IsLoggedIn)
			.FilterOnObservable(x => x.State.Select(s => s == WalletWasabi.Wallets.WalletState.Started))
			.MergeMany(x => x.Transactions.NewTransactionArrived)
			.Where(x => !UiContext.ApplicationSettings.PrivacyMode)
			.Where(x => x.EventArgs.IsNews)
			.DoAsync(x => OnNotificationReceivedAsync(x.Wallet, x.EventArgs))
			.Subscribe();
	}

	private async Task OnNotificationReceivedAsync(WalletModel wallet, ProcessedResult e)
	{
		if (!e.IsOwnCoinJoin)
		{
			void OnClick()
			{
				if (UiContext.Navigate().IsAnyPageBusy)
				{
					return;
				}

				var wvm = _navBar.Select(wallet);
				wvm?.SelectTransaction(e.Transaction.GetHash());
			}

			NotificationHelpers.Show(wallet, e, OnClick);
		}

		if (_navBar.SelectedWalletModel == wallet && (e.NewlyReceivedCoins.Count != 0 || e.NewlyConfirmedReceivedCoins.Count != 0))
		{
			await Task.Delay(200);
			_navBar.SelectedWallet?.WalletViewModel?.SelectTransaction(e.Transaction.GetHash());
		}
	}
}
