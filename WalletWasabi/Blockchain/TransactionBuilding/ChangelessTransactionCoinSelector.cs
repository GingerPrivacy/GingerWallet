using CoinSelection;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public static class ChangelessTransactionCoinSelector
{
	public static async IAsyncEnumerable<IReadOnlyList<SmartCoin>> GetAllStrategyResultsAsync(
		IReadOnlyList<SmartCoin> availableCoins,
		FeeRate feeRate,
		TxOut txOut,
		int maxInputCount,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (maxInputCount <= 0)
		{
			yield break;
		}

		long target = txOut.Value.Satoshi + feeRate.GetFee(txOut.ScriptPubKey.EstimateOutputVsize()).Satoshi;

		// Spend all coins sharing a script together; the input limit counts these groups.
		var candidates = availableCoins
			.GroupBy(coin => coin.ScriptPubKey)
			.Select(group => new SelectionCandidate<SmartCoin[]>(
				group.ToArray(),
				group.Sum(coin => coin.EffectiveValue(feeRate).Satoshi),
				group.Sum(coin => feeRate.GetFee(coin.ScriptPubKey.EstimateInputVsize()).Satoshi)))
			.Where(candidate => candidate.Value > 0)
			.OrderByDescending(candidate => candidate.Value)
			.ToArray();

		SelectionOptions options = new()
		{
			MaximumCandidateCount = maxInputCount,
			MaximumExcess = (long)(target * 1.25) - target,
			MaximumShortfall = target - (long)(target * 0.75),
			// The caller's cancellation token already limits the search time.
			MaximumSearchNodes = long.MaxValue
		};
		var selections = await Task.Run(() => CoinSelector.SelectBoth(candidates, target, options, cancellationToken), cancellationToken).ConfigureAwait(false);

		foreach (var selection in new[] { selections.AtLeastTarget, selections.AtMostTarget })
		{
			if (selection.Status == SelectionStatus.Cancelled)
			{
				Logger.LogInfo("Computing privacy suggestions was cancelled or timed out.");
			}

			if (selection.HasSelection)
			{
				yield return selection.SelectedCandidates.SelectMany(candidate => candidate.Item).ToArray();
			}
		}
	}
}
