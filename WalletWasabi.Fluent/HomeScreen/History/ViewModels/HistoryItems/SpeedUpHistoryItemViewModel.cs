using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.History.ViewModels.HistoryItems;

public class SpeedUpHistoryItemViewModel : HistoryItemViewModelBase
{
	public SpeedUpHistoryItemViewModel(WalletModel wallet, TransactionModel transaction, HomeScreen.History.ViewModels.HistoryItems.HistoryItemViewModelBase? parent) : base(transaction)
	{
		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().TransactionDetails(wallet, transaction));
		CancelTransactionCommand = parent?.CancelTransactionCommand;
	}

	public bool TransactionOperationsVisible => Transaction.CanCancelTransaction;
}
