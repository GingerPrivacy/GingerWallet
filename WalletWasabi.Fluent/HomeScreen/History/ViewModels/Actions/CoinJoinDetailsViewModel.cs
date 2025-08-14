using System.Reactive.Disposables;
using NBitcoin;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.History.ViewModels.Actions;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinJoinDetailsViewModel : RoutableViewModel
{
	private readonly WalletModel _wallet;
	private readonly TransactionModel _transaction;

	[AutoNotify] private string _date = "";
	[AutoNotify] private Amount? _coinJoinFeeAmount;
	[AutoNotify] private uint256? _transactionId;
	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private int _confirmations;
	[AutoNotify] private TimeSpan? _confirmationTime;
	[AutoNotify] private bool _isConfirmationTimeVisible;
	[AutoNotify] private FeeRate? _feeRate;
	[AutoNotify] private bool _feeRateVisible;

	public CoinJoinDetailsViewModel(WalletModel wallet, TransactionModel transaction)
	{
		Title = Resources.CoinjoinDetails;
		_wallet = wallet;
		_transaction = transaction;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = CancelCommand;
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_wallet.Transactions.Cache
			.Connect()
			.Subscribe(_ => Update())
			.DisposeWith(disposables);
	}

	private void Update()
	{
		if (_wallet.Transactions.TryGetById(_transaction.Id, _transaction.IsChild, out var transaction))
		{
			Date = transaction.DateToolTipString;
			CoinJoinFeeAmount = _wallet.AmountProvider.Create((Money)Math.Abs(transaction.DisplayAmount));
			Confirmations = transaction.Confirmations;
			IsConfirmed = Confirmations > 0;
			TransactionId = transaction.Id;
			ConfirmationTime = _wallet.Transactions.TryEstimateConfirmationTime(transaction.Id);
			IsConfirmationTimeVisible = ConfirmationTime.HasValue && ConfirmationTime != TimeSpan.Zero;
			FeeRate = transaction.FeeRate;
			FeeRateVisible = FeeRate != FeeRate.Zero;
		}
	}
}
