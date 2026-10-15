using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.Models.Transactions;

public record TransactionCoinRow(OutPoint OutPoint, string Address, Money? Amount, bool IsOwn, bool IsChange, LabelsArray Labels)
{
	public Script? ScriptPubKey { get; init; }
	public int? AnonymityScore { get; init; }
	public string AmountText => Amount is { } amount ? $"{amount.ToFormattedString()} BTC" : Resources.Unknown;
	public string Ownership => IsChange ? Resources.TransactionCoinChange : IsOwn ? Resources.TransactionCoinOwn : Resources.TransactionCoinExternal;
}

public class TransactionCoinsModel
{
	public TransactionCoinsModel(BuildTransactionResult result, Network network, Func<Script, bool> isOwn, IReadOnlyDictionary<Script, LabelsArray> paymentLabels)
		: this(result.Transaction, network, isOwn, paymentLabels.Keys.Concat(result.OuterWalletOutputs.Select(x => x.ScriptPubKey)))
	{
		IsPreview = true;
		VirtualSize = result.Psbt.TryGetVirtualSize(out var size) ? size : VirtualSize;
		Fee = result.Fee;
		var pendingLabels = result.Transaction.WalletOutputs.Where(coin => isOwn(coin.ScriptPubKey)).ToDictionary(
			coin => coin.Index, coin => result.HdPubKeysWithNewLabels.GetValueOrDefault(coin.HdPubKey, coin.HdPubKey.Labels));
		Outputs = Outputs.Select(row => row with
		{
			// New outputs have not been analyzed yet; an existing key's score is not a prediction.
			AnonymityScore = null,
			Labels = paymentLabels.TryGetValue(row.ScriptPubKey!, out var labels)
				? labels : pendingLabels.GetValueOrDefault(row.OutPoint.N, row.Labels)
		}).ToArray();
	}

	public TransactionCoinsModel(SmartTransaction transaction, Network network, Func<Script, bool> isOwn, IEnumerable<Script>? paymentScripts = null)
	{
		Transaction = transaction;
		VirtualSize = transaction.Transaction.GetVirtualSize();
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
				coin?.Amount, own, false, own ? coin!.HdPubKey.Labels : LabelsArray.Empty)
			{
				ScriptPubKey = coin?.ScriptPubKey,
				AnonymityScore = GetAnonymityScore(own ? coin : null)
			};
		}).ToArray();
		Outputs = transaction.Transaction.Outputs.Select((output, index) =>
		{
			knownOutputs.TryGetValue((uint)index, out var coin);
			var own = isOwn(output.ScriptPubKey);
			return new TransactionCoinRow(new OutPoint(transaction.GetHash(), (uint)index),
				output.ScriptPubKey.GetDestinationAddress(network)?.ToString() ?? output.ScriptPubKey.ToHex(),
				output.Value, own, paymentScripts is not null && own && spendsOwnCoins && coin?.HdPubKey.IsInternal is true && !recipients.Contains(output.ScriptPubKey),
				own && coin is { } ? coin.HdPubKey.Labels : LabelsArray.Empty)
			{
				ScriptPubKey = output.ScriptPubKey,
				AnonymityScore = GetAnonymityScore(own ? coin : null)
			};
		}).ToArray();
		Fee = Inputs.All(i => i.Amount is not null) ? Inputs.Sum(i => i.Amount) - Outputs.Sum(o => o.Amount) : null;
	}

	public SmartTransaction Transaction { get; }
	public bool IsPreview { get; }
	public int VirtualSize { get; }
	public Money? Fee { get; }
	public IReadOnlyList<TransactionCoinRow> Inputs { get; }
	public IReadOnlyList<TransactionCoinRow> Outputs { get; }

	private static int? GetAnonymityScore(SmartCoin? coin) =>
		coin is { AnonymitySet: >= 1 and < HdPubKey.DefaultHighAnonymitySet } ? (int)coin.AnonymitySet : null;
}
