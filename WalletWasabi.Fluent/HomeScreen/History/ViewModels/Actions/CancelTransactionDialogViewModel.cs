using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.HomeScreen.History.ViewModels.Actions;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class CancelTransactionDialogViewModel : RoutableViewModel
{
	private readonly WalletModel _wallet;
	private readonly CancellingTransaction _cancellingTransaction;

	public CancelTransactionDialogViewModel(WalletModel wallet, CancellingTransaction cancellingTransaction)
	{
		Title = Resources.CancelTransaction;
		_wallet = wallet;
		_cancellingTransaction = cancellingTransaction;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		Fee = cancellingTransaction.Fee;

		EnableBack = false;
		NextCommand = ReactiveCommand.CreateFromTask(() => OnCancelTransactionAsync(cancellingTransaction));
	}

	public Amount Fee { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		// Close dialog if target transaction is already confirmed.
		_wallet.Transactions.Cache
			.Watch(_cancellingTransaction.TargetTransaction.Id)
			.Where(change => change.Current.IsConfirmed)
			.Do(_ => UiContext.Navigate(CurrentTarget).Back())
			.Subscribe()
			.DisposeWith(disposables);

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private async Task OnCancelTransactionAsync(CancellingTransaction cancellingTransaction)
	{
		IsBusy = true;

		try
		{
			var isAuthorized = await AuthorizeForPasswordAsync();
			if (isAuthorized)
			{
				await _wallet.Transactions.SendAsync(cancellingTransaction);
				UiContext.Navigate().To().SendSuccess(cancellingTransaction.CancelTransaction.Transaction, NavigationTarget.CompactDialogScreen);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			var msg = cancellingTransaction.TargetTransaction.IsConfirmed ? Resources.TransactionAlreadyConfirmed : ex.ToUserFriendlyString();
			UiContext.Navigate().To().ShowErrorDialog(msg, Resources.CancellationFailed, Resources.GingerWalletUnableToCancelTransaction, NavigationTarget.CompactDialogScreen);
		}

		IsBusy = false;
	}

	private async Task<bool> AuthorizeForPasswordAsync()
	{
		if (_wallet.Auth.HasPassword)
		{
			return await UiContext.Navigate().To().PasswordAuthDialog(_wallet, Resources.WalletSend).GetResultAsync();
		}

		return true;
	}
}
