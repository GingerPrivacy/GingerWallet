using CoinSelection;
using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding.BnB;
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
		// target = target amount + output cost
		long target = txOut.Value.Satoshi + feeRate.GetFee(txOut.ScriptPubKey.EstimateOutputVsize()).Satoshi;

		// Group coins by their script pub key and sort the groups in the descending order. Each coin group is considered to be a single coin for the purposes of the algorithm.
		// All coins in a single group have the same script pub key so all the coins should be spent together or not spent at all.
		IOrderedEnumerable<IGrouping<Script, SmartCoin>> coinsByScript = availableCoins
			.GroupBy(coin => coin.ScriptPubKey)
			.OrderByDescending(group => group.Sum(coin => coin.EffectiveValue(feeRate).Satoshi));

		// How much it costs to spend each coin group.
		long[] inputCosts = coinsByScript.Select(group => group.Sum(coin => feeRate.GetFee(coin.ScriptPubKey.EstimateInputVsize()).Satoshi)).ToArray();

		Dictionary<SmartCoin[], long> inputEffectiveValues = new(coinsByScript.ToDictionary(x => x.Select(coin => coin).ToArray(), x => x.Sum(coin => coin.EffectiveValue(feeRate).Satoshi)));

		// Pass smart coin groups effective values in descending order.
		long[] inputValues = inputEffectiveValues.Values.ToArray();

		StrategyParameters parameters = new(target, inputValues, inputCosts, maxInputCount);

		SelectionStrategy[] strategies = new SelectionStrategy[]
		{
			new MoreSelectionStrategy(parameters),
			new LessSelectionStrategy(parameters)
		};

		var tasks = strategies
			.Select(strategy => Task.Run(
				() =>
				{
					if (TryGetCoins(strategy, inputEffectiveValues, out IReadOnlyList<SmartCoin>? coins, cancellationToken))
					{
						return coins;
					}

					return null;
				},
				cancellationToken))
			.ToArray();

		foreach (var task in tasks)
		{
			IReadOnlyList<SmartCoin>? smartCoins = await task.ConfigureAwait(false);

			if (smartCoins is not null)
			{
				yield return smartCoins;
			}
		}
	}

	/// <summary>
	/// Select coins in a way that user can pay without a change output (to increase privacy)
	/// and try to find a solution that requires to pay as little extra amount as possible.
	/// </summary>
	/// <param name="strategy">The strategy determines what the algorithm is looking for.</param>
	/// <param name="inputEffectiveValues">Dictionary to map back the effective values to their original SmartCoin. </param>
	/// <param name="selectedCoins">Out parameter that returns (non-grouped!) coins back.</param>
	/// <returns><c>true</c> if a solution was found, <c>false</c> otherwise.</returns>
	internal static bool TryGetCoins(SelectionStrategy strategy, Dictionary<SmartCoin[], long> inputEffectiveValues, [NotNullWhen(true)] out IReadOnlyList<SmartCoin>? selectedCoins, CancellationToken cancellationToken)
	{
		selectedCoins = null;

		if (strategy.Parameters.MaxInputCount <= 0)
		{
			return false;
		}

		// Keep the wallet's fee estimates, inclusive payment bounds and script-group input limit.
		SelectionOptions options = new()
		{
			Mode = strategy is MoreSelectionStrategy ? SelectionMode.AtLeastTarget : SelectionMode.AtMostTarget,
			MaximumDifference = strategy switch
			{
				MoreSelectionStrategy more => more.MaximumTarget - strategy.Target,
				LessSelectionStrategy less => strategy.Target - less.MinimumTarget,
				_ => throw new ArgumentException("Unsupported changeless selection strategy.", nameof(strategy))
			},
			MaximumInputCount = strategy.Parameters.MaxInputCount,
			// The caller's cancellation token already limits the search time.
			MaximumSearchNodes = long.MaxValue
		};

		var candidates = inputEffectiveValues
			.Select((group, i) => new SelectionCandidate<SmartCoin[]>(group.Key, group.Value, strategy.InputCosts[i]))
			.Where(candidate => candidate.Value > 0)
			.ToArray();

		var selection = CoinSelector.Select(candidates, strategy.Target, options, cancellationToken);
		if (selection.Status == SelectionStatus.Cancelled)
		{
			Logger.LogInfo("Computing privacy suggestions was cancelled or timed out.");
		}

		if (selection.HasSelection)
		{
			selectedCoins = selection.SelectedCandidates.SelectMany(candidate => candidate.Item).ToArray();
			return true;
		}

		return false;
	}
}
