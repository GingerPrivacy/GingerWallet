using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.HomeScreen.History.ViewModels.Actions;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class SpeedUpTransactionDialogViewModel : RoutableViewModel
{
	private readonly SpeedupTransaction _speedupTransaction;
	private readonly IWalletModel _wallet;

	public SpeedUpTransactionDialogViewModel(IWalletModel wallet, SpeedupTransaction speedupTransaction)
	{
		Title = Resources.SpeedUpTransaction;

		_wallet = wallet;
		_speedupTransaction = speedupTransaction;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;
		NextCommand = ReactiveCommand.CreateFromTask(() => OnSpeedUpTransactionAsync(speedupTransaction));

		Fee = speedupTransaction.Fee;
		AreWePayingTheFee = speedupTransaction.AreWePayingTheFee;
	}

	public Amount Fee { get; }

	public bool AreWePayingTheFee { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		// Close dialog if target transaction is already confirmed.
		_wallet.Transactions.Cache
			.Connect()
			.Watch(_speedupTransaction.TargetTransaction.GetHash())
			.Where(change => change.Current.IsConfirmed)
			.Do(_ => Navigate().Back())
			.Subscribe()
			.DisposeWith(disposables);

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private async Task OnSpeedUpTransactionAsync(SpeedupTransaction speedupTransaction)
	{
		IsBusy = true;

		try
		{
			var isAuthorized = await AuthorizeForPasswordAsync();
			if (isAuthorized)
			{
				await _wallet.Transactions.SendAsync(speedupTransaction);
				UiContext.Navigate().To().SendSuccess(speedupTransaction.BoostingTransaction.Transaction, NavigationTarget.CompactDialogScreen);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			var msg = speedupTransaction.TargetTransaction.Confirmed ? Resources.TransactionAlreadyConfirmed : ex.ToUserFriendlyString();
			UiContext.Navigate().To().ShowErrorDialog(msg, Resources.SpeedUpFailed, Resources.GingerWalletUnableToSpeedUpTransaction, NavigationTarget.CompactDialogScreen);
		}

		IsBusy = false;
	}

	private async Task<bool> AuthorizeForPasswordAsync()
	{
		if (_wallet.Auth.HasPassword)
		{
			return await Navigate().To().PasswordAuthDialog(_wallet, Resources.WalletSend).GetResultAsync();
		}

		return true;
	}
}
