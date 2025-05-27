using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.History.ViewModels.HistoryItems;

public class CoinJoinHistoryItemViewModel : HistoryItemViewModelBase
{
	public CoinJoinHistoryItemViewModel(WalletModel wallet, TransactionModel transaction) : base(transaction)
	{
		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().CoinJoinDetails(wallet, transaction));
		CanOpenInBrowser = true;
	}
}
