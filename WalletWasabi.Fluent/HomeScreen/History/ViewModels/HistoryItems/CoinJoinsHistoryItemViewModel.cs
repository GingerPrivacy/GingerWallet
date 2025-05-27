using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.History.ViewModels.HistoryItems;

public class CoinJoinsHistoryItemViewModel : HistoryItemViewModelBase
{
	public CoinJoinsHistoryItemViewModel(WalletModel wallet, TransactionModel transaction) : base(transaction)
	{
		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().CoinJoinsDetails(wallet, transaction));
	}
}
