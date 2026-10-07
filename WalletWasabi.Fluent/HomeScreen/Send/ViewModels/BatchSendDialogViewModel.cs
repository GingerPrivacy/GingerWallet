using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.HomeScreen.Send.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.Send.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class BatchSendDialogViewModel : DialogViewModelBase<TransactionInfo?>
{
	private readonly WalletModel _wallet;
	private readonly Money _availableAmount;
	[AutoNotify] private bool _canContinue;
	[AutoNotify] private bool _subtractFee;
	[AutoNotify] private string? _error;

	public BatchSendDialogViewModel(WalletModel wallet, Money availableAmount, string address, decimal? amount, LabelsArray labels)
	{
		_wallet = wallet;
		_availableAmount = availableAmount;
		Title = Resources.BatchPayments;
		SetupCancel(true, true, true);
		EnableBack = true;
		AddRecipientCommand = ReactiveCommand.Create(AddRecipient);
		NextCommand = ReactiveCommand.Create(OnNext, this.WhenAnyValue(x => x.CanContinue));
		var first = AddRecipient();
		first.To = address;
		first.AmountBtc = amount;
		foreach (var label in labels)
		{
			first.SuggestionLabels.Labels.Add(label);
		}
		SubtractFee = amount == availableAmount.ToDecimal(MoneyUnit.BTC);
		AddRecipient();
	}

	public ObservableCollection<RecipientRowViewModel> Recipients { get; } = new();
	public ICommand AddRecipientCommand { get; }

	private RecipientRowViewModel AddRecipient()
	{
		var row = new RecipientRowViewModel(_wallet, ValidateRecipients, RemoveRecipient, InsertMax);
		Recipients.Add(row);
		ValidateRecipients();
		return row;
	}

	private void RemoveRecipient(RecipientRowViewModel row)
	{
		if (Recipients.Count > 2 && Recipients.Remove(row))
		{
			row.Dispose();
			ValidateRecipients();
		}
	}

	private void InsertMax(RecipientRowViewModel row)
	{
		var otherAmounts = Recipients.Where(x => x != row).Sum(x => x.AmountBtc ?? 0);
		row.AmountBtc = Math.Max(0, _availableAmount.ToDecimal(MoneyUnit.BTC) - otherAmounts);
		SubtractFee = true;
	}

	private IReadOnlyList<RecipientInfo>? GetRecipients()
	{
		var recipients = new List<RecipientInfo>();
		var valid = true;
		foreach (var row in Recipients)
		{
			if (row.TryGetRecipient(out var recipient) && recipient is { })
			{
				recipients.Add(recipient);
			}
			else
			{
				valid = false;
			}
		}
		return valid ? recipients : null;
	}

	private void ValidateRecipients()
	{
		foreach (var row in Recipients)
		{
			row.CanRemove = Recipients.Count > 2;
		}
		Error = null;
		var recipients = GetRecipients();
		if (recipients is { })
		{
			if (recipients.Select(x => x.Destination.ScriptPubKey).Distinct().Count() != recipients.Count)
			{
				Error = Resources.BatchDuplicateAddress;
			}
			else if (recipients.Sum(x => x.Amount) > _availableAmount)
			{
				Error = Resources.InsufficientFunds;
			}
		}
		CanContinue = recipients is { Count: >= 2 } && Error is null;
	}

	private void OnNext()
	{
		ValidateRecipients();
		if (!CanContinue || GetRecipients() is not { } recipients)
		{
			return;
		}
		var first = recipients[0];
		if (first.Destination is not BitcoinAddress destination)
		{
			return;
		}
		Close(DialogResultKind.Normal, new TransactionInfo(destination, _wallet.Settings.AnonScoreTarget)
		{
			Amount = first.Amount,
			Recipient = first.Label,
			AdditionalRecipients = recipients.Skip(1).ToArray(),
			SubtractFee = SubtractFee,
			IsFixedAmount = true
		});
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		base.OnNavigatedFrom(isInHistory);
		if (!isInHistory)
		{
			foreach (var row in Recipients)
			{
				row.Dispose();
			}
		}
	}
}
