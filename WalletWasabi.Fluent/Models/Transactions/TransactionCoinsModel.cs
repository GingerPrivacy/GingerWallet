using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.Models.Transactions;

public record TransactionCoinRow(OutPoint OutPoint, string Address, Money? Amount, bool IsOwn, bool IsChange, LabelsArray Labels)
{
	public string AmountText => Amount is { } amount ? $"{amount.ToFormattedString()} BTC" : Resources.Unknown;
	public string Ownership => IsChange ? Resources.TransactionCoinChange : IsOwn ? Resources.TransactionCoinOwn : Resources.TransactionCoinExternal;
}

public class TransactionCoinsModel
{
	public TransactionCoinsModel(BuildTransactionResult result, Network network, Func<Script, bool> isOwn, IEnumerable<Script> paymentScripts)
		: this(result.Transaction, network, isOwn, paymentScripts.Concat(result.OuterWalletOutputs.Select(x => x.ScriptPubKey)))
	{
	}

	public TransactionCoinsModel(SmartTransaction transaction, Network network, Func<Script, bool> isOwn, IEnumerable<Script>? paymentScripts = null)
	{
		var recipients = (paymentScripts ?? Array.Empty<Script>()).ToHashSet();
		var knownInputs = transaction.WalletInputs.ToDictionary(x => x.Outpoint);
		var knownOutputs = transaction.WalletOutputs.ToDictionary(x => x.Index);
		var spendsOwnCoins = transaction.WalletInputs.Any(x => isOwn(x.ScriptPubKey));
		Inputs = transaction.Transaction.Inputs.Select(input =>
		{
			knownInputs.TryGetValue(input.PrevOut, out var coin);
			var own = coin is { } && isOwn(coin.ScriptPubKey);
			return new TransactionCoinRow(input.PrevOut,
				coin?.ScriptPubKey.GetDestinationAddress(network)?.ToString() ?? Resources.Unknown,
				coin?.Amount, own, false, own ? coin!.HdPubKey.Labels : LabelsArray.Empty);
		}).ToArray();
		Outputs = transaction.Transaction.Outputs.Select((output, index) =>
		{
			knownOutputs.TryGetValue((uint)index, out var coin);
			var own = isOwn(output.ScriptPubKey);
			return new TransactionCoinRow(new OutPoint(transaction.GetHash(), (uint)index),
				output.ScriptPubKey.GetDestinationAddress(network)?.ToString() ?? output.ScriptPubKey.ToHex(),
				output.Value, own, own && spendsOwnCoins && coin?.HdPubKey.IsInternal is true && !recipients.Contains(output.ScriptPubKey),
				own && coin is { } ? coin.HdPubKey.Labels : LabelsArray.Empty);
		}).ToArray();
	}

	public IReadOnlyList<TransactionCoinRow> Inputs { get; }
	public IReadOnlyList<TransactionCoinRow> Outputs { get; }
}
