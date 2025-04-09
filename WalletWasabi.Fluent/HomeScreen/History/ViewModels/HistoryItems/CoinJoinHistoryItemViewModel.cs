using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.History.ViewModels.HistoryItems;

public partial class CoinJoinHistoryItemViewModel : HistoryItemViewModelBase
{
	public CoinJoinHistoryItemViewModel(IWalletModel wallet, TransactionModel transaction) : base(transaction)
	{
		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().CoinJoinDetails(wallet, transaction));
		CanOpenInBrowser = true;
	}
}
