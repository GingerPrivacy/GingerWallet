using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.History.ViewModels.HistoryItems;

public partial class CoinJoinsHistoryItemViewModel : HistoryItemViewModelBase
{
	private CoinJoinsHistoryItemViewModel(IWalletModel wallet, TransactionModel transaction) : base(transaction)
	{
		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().CoinJoinsDetails(wallet, transaction));
	}
}
