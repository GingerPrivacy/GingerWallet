using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.HomeScreen.Send.Models;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.Send.ViewModels;

public partial class TransactionSummaryViewModel : ViewModelBase
{
	private readonly WalletModel _wallet;
	private BuildTransactionResult? _transaction;
	[AutoNotify] private bool _transactionHasChange;
	[AutoNotify] private TimeSpan? _confirmationTime;
	[AutoNotify] private string _feeText = "";
	[AutoNotify] private bool _isCustomFeeUsed;
	[AutoNotify] private bool _isOtherPocketSelectionPossible;
	[AutoNotify] private LabelsArray _labels = LabelsArray.Empty;
	[AutoNotify] private LabelsArray _recipient = LabelsArray.Empty;
	[AutoNotify] private Amount? _fee;
	[AutoNotify] private Amount? _amount;
	[AutoNotify] private double? _amountDiff;
	[AutoNotify] private double? _feeDiff;

	public TransactionSummaryViewModel(TransactionPreviewViewModel parent, WalletModel wallet, TransactionInfo info, bool isPreview = false)
	{
		Parent = parent;
		_wallet = wallet;
		IsPreview = isPreview;
		AddressText = info.Destination.ToString();
		PayJoinUrl = info.PayJoinClient?.PaymentUrl.AbsoluteUri;
		IsPayJoin = PayJoinUrl is not null;
	}

	public TransactionPreviewViewModel Parent { get; }

	public bool IsPreview { get; }

	public string AddressText { get; }

	public string? PayJoinUrl { get; }

	public bool IsPayJoin { get; }

	public void UpdateTransaction(BuildTransactionResult transactionResult, TransactionInfo info)
	{
		_transaction = transactionResult;

		ConfirmationTime = _wallet.Transactions.TryEstimateConfirmationTime(info);

		var destinationAmount = _transaction.CalculateDestinationAmount(info.Destination);

		Amount = UiContext.AmountProvider.Create(destinationAmount);
		Fee = UiContext.AmountProvider.Create(_transaction.Fee);

		Recipient = info.Recipient;
		IsCustomFeeUsed = info.IsCustomFeeUsed;
		IsOtherPocketSelectionPossible = info.IsOtherPocketSelectionPossible;
		AmountDiff = DiffOrNull(Amount, Parent.CurrentTransactionSummary.Amount);
		FeeDiff = DiffOrNull(Fee, Parent.CurrentTransactionSummary.Fee);
	}

	private static double? DiffOrNull(Amount? current, Amount? previous)
	{
		if (current is null || previous is null)
		{
			return null;
		}

		return current.Diff(previous);
	}
}
