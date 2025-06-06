using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Lang;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.HomeScreen.History.ViewModels.HistoryItems;

public class TransactionHistoryItemViewModel : HistoryItemViewModelBase
{
	private WalletModel _wallet;

	public TransactionHistoryItemViewModel(WalletModel wallet, TransactionModel transaction) : base(transaction)
	{
		_wallet = wallet;

		CanBeSpedUp = transaction.CanSpeedUpTransaction && !IsChild;
		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().TransactionDetails(wallet, transaction));
		SpeedUpTransactionCommand = ReactiveCommand.Create(() => OnSpeedUpTransaction(transaction), Observable.Return(CanBeSpedUp));
		CancelTransactionCommand = ReactiveCommand.Create(() => OnCancelTransaction(transaction), Observable.Return(transaction.CanCancelTransaction));
		HasBeenSpedUp = transaction.HasBeenSpedUp;
		CanOpenInBrowser = true;
	}

	public bool TransactionOperationsVisible => Transaction.CanCancelTransaction || CanBeSpedUp;

	private void OnSpeedUpTransaction(TransactionModel transaction)
	{
		try
		{
			var speedupTransaction = _wallet.Transactions.CreateSpeedUpTransaction(transaction);
			UiContext.Navigate().To().SpeedUpTransactionDialog(_wallet, speedupTransaction);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), Resources.SpeedUpFailed, Resources.GingerWalletUnableToSpeedUpTransaction);
		}
	}

	private void OnCancelTransaction(TransactionModel transaction)
	{
		try
		{
			var cancellingTransaction = _wallet.Transactions.CreateCancellingTransaction(transaction);
			UiContext.Navigate().To().CancelTransactionDialog(_wallet, cancellingTransaction);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), Resources.CancellationFailed, Resources.GingerWalletUnableToCancelTransaction);
		}
	}
}
