using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;

/// <summary>
/// Pull requests to this file must be up to date with this simulation to ensure correctness: https://github.com/nopara73/Sake
/// </summary>
public class AmountDecomposer
{
	/// <param name="feeRate">Bitcoin network fee rate the coinjoin is targeting.</param>
	/// <param name="minAllowedOutputAmount">Min output amount that's allowed to be registered.</param>
	/// <param name="maxAllowedOutputAmount">Max output amount that's allowed to be registered.</param>
	/// <param name="availableVsize">Available virtual size for outputs.</param>
	/// <param name="random">Allows testing by setting a seed value for the random number generator.</param>
	public AmountDecomposer(FeeRate feeRate, Money minAllowedOutputAmount, Money maxAllowedOutputAmount, int availableVsize, IEnumerable<ScriptType> allowedOutputTypes, WasabiRandom random)
	{
		FeeRate = feeRate;

		AvailableVsize = availableVsize;
		AllowedOutputTypes = allowedOutputTypes;
		MinAllowedOutputAmount = minAllowedOutputAmount;
		MaxAllowedOutputAmount = maxAllowedOutputAmount;
		Random = random;

		var scriptWithSmallestOutput = allowedOutputTypes.OrderBy(x => x.EstimateOutputVsize()).First();
		MinimumOutputSize = scriptWithSmallestOutput.EstimateOutputVsize();
		MinimumOutputFee = FeeRate.GetFee(MinimumOutputSize);
		FutureInputFeeForMinimumOutputFee = FeeRate.GetFee(scriptWithSmallestOutput.EstimateInputVsize());
		MaximumOutputNumber = MinimumOutputSize > 0 ? AvailableVsize / MinimumOutputSize : AvailableVsize;
	}

	public int MinimumOutputSize { get; }
	public Money MinimumOutputFee { get; }
	public Money FutureInputFeeForMinimumOutputFee { get; }
	public int MaximumOutputNumber { get; }

	public FeeRate FeeRate { get; }
	public int AvailableVsize { get; }
	public IEnumerable<ScriptType> AllowedOutputTypes { get; }
	public Money MinAllowedOutputAmount { get; }
	public Money MaxAllowedOutputAmount { get; }

	private WasabiRandom Random { get; }

	public IEnumerable<Output> Decompose(Money myInputSum, List<Money> denoms)
	{
		var maxNumberOfOutputsAllowed = Math.Min(MaximumOutputNumber, 10);
		var maxNumberOfOutputsAllowedForChangeless = Math.Min(MaximumOutputNumber, Math.Max(maxNumberOfOutputsAllowed, 12));

		// If there are no output denominations, the participation in coinjoin makes no sense.
		if (!denoms.Any())
		{
			throw new InvalidOperationException(
				"No valid output denominations found. This can occur when an insufficient number of coins are registered to participate in the coinjoin.");
		}

		// If my input sum is smaller than the smallest denomination, then participation in a coinjoin makes no sense.
		if (denoms.Last() + MinimumOutputFee > myInputSum)
		{
			throw new InvalidOperationException("Not enough coins registered to participate in the coinjoin.");
		}

		var setCandidates = new Dictionary<int, List<Money>>();

		// Create the most naive decomposition for starter.
		var naiveDecomp = CreateNaiveDecomposition(denoms, myInputSum, maxNumberOfOutputsAllowed);
		setCandidates.Add(naiveDecomp.Key, naiveDecomp.Value);

		// Create more pre-decompositions for sanity.
		var preDecomps = CreatePreDecompositions(denoms, myInputSum, maxNumberOfOutputsAllowed);
		foreach (var decomp in preDecomps)
		{
			setCandidates.TryAdd(decomp.Key, decomp.Value);
		}

		// Create many decompositions for optimization.
		var changelessDecomps = CreateChangelessDecompositions(denoms, myInputSum, maxNumberOfOutputsAllowed, maxNumberOfOutputsAllowedForChangeless);
		foreach (var decomp in changelessDecomps)
		{
			setCandidates.TryAdd(decomp.Key, decomp.Value);
		}

		var denomHashSet = denoms.ToHashSet();
		var preCandidates = setCandidates.Select(x => x.Value).ToList();

		// If there are changeless candidates, don't even consider ones with change.
		var changelessCandidates = preCandidates.Where(x => x.All(y => denomHashSet.Contains(y))).ToList();
		var changeAvoided = changelessCandidates.Count != 0;
		if (changeAvoided)
		{
			preCandidates = changelessCandidates;
		}
		preCandidates.Shuffle(Random);

		// Create Outputs from it
		var outputCandidates = preCandidates.Select(x =>
		{
			var output = CreateOutputs(x, myInputSum, denoms);
			var cost = myInputSum - output.Sum(x => x.Amount - x.InputFee);
			return (Decomposition: output, Cost: cost);
		}).Where(x => x.Decomposition.Length > 0);

		var orderedCandidates = outputCandidates
			.OrderBy(x => GetChange(x.Decomposition, denomHashSet)) // Less change is better.
			.ThenBy(x => x.Cost) // Less cost is better.
			.ThenBy(x => x.Decomposition.Any(d => d.ScriptType == ScriptType.Taproot) && x.Decomposition.Any(d => d.ScriptType == ScriptType.P2WPKH) ? 0 : 1) // Prefer mixed scripts types.
			.Select(x => x).ToList();

		// We want to introduce randomness between the best selections.
		// If we successfully avoided change, then what matters is cost,
		// if we didn't then cost calculation is irrelevant, because the size of change is more costly.
		(Output[] Decomp, Money Cost)[] finalCandidates;
		if (changeAvoided)
		{
			var bestCandidateCost = orderedCandidates.First().Cost;
			var costTolerance = Money.Coins(bestCandidateCost.ToUnit(MoneyUnit.BTC) * 1.2m);
			finalCandidates = orderedCandidates.Where(x => x.Cost <= costTolerance).ToArray();
		}
		else
		{
			// Change can only be max between: 100.000 satoshis, 10% of the inputs sum or 20% more than the best candidate change
			var bestCandidateChange = GetChange(orderedCandidates.First().Decomposition, denomHashSet);
			var changeTolerance = Money.Coins(
				Math.Max(
					Math.Max(
						myInputSum.ToUnit(MoneyUnit.BTC) * 0.1m,
						bestCandidateChange.ToUnit(MoneyUnit.BTC) * 1.2m),
					Money.Satoshis(100000).ToUnit(MoneyUnit.BTC)));

			finalCandidates = orderedCandidates.Where(x => GetChange(x.Decomposition, denomHashSet) <= changeTolerance).ToArray();
		}

		// We want to make sure our random selection is not between similar decompositions.
		// Different largest elements result in very different decompositions.
		var largestAmount = finalCandidates.Select(x => x.Decomp.First()).ToHashSet().RandomElement(Random);
		var finalCandidate = finalCandidates.Where(x => x.Decomp.First() == largestAmount).RandomElement(Random).Decomp;

		var totalOutputAmount = finalCandidate.Sum(x => x.EffectiveCost);
		if (totalOutputAmount > myInputSum)
		{
			throw new InvalidOperationException("The decomposer is creating money. Aborting.");
		}
		if (totalOutputAmount + MinAllowedOutputAmount < myInputSum)
		{
			throw new InvalidOperationException("The decomposer is losing money. Aborting.");
		}

		var totalOutputVsize = finalCandidate.Sum(d => d.ScriptType.EstimateOutputVsize());
		if (totalOutputVsize > AvailableVsize)
		{
			throw new InvalidOperationException("The decomposer created more outputs than it can. Aborting.");
		}
		return finalCandidate;
	}

	// The change is always the last element
	private static Money GetChange(Output[] decomposition, HashSet<Money> denomHashSet)
	{
		return decomposition.Length == 0 || denomHashSet.Contains(decomposition[^1].Amount) ? Money.Zero : decomposition[^1].Amount;
	}

	private IDictionary<int, List<Money>> CreateChangelessDecompositions(List<Money> denoms, Money myInputSum, int preferedNumberOfOutputsAllowed, int maxNumberOfOutputsAllowed)
	{
		var candidates = CreateChangelessDecompositions(denoms, myInputSum, preferedNumberOfOutputsAllowed);
		return candidates.Count > 0 || preferedNumberOfOutputsAllowed >= maxNumberOfOutputsAllowed ? candidates : CreateChangelessDecompositions(denoms, myInputSum, maxNumberOfOutputsAllowed);
	}

	private IDictionary<int, List<Money>> CreateChangelessDecompositions(List<Money> denoms, Money myInputSum, int maxNumberOfOutputsAllowed)
	{
		var setCandidates = new Dictionary<int, List<Money>>();

		if (maxNumberOfOutputsAllowed > 1)
		{
			var pureDenoms = denoms.Select(x => x.Satoshi).ToArray();
			var decompDenoms = denoms.Select(x => x.Satoshi + MinimumOutputFee.Satoshi).ToArray();
			foreach (var (sum, count, decomp) in Decomposer.Decompose((long)myInputSum, MinAllowedOutputAmount, Math.Min(maxNumberOfOutputsAllowed, 12), decompDenoms))
			{
				var currentSet = Decomposer.ToRealValuesArray(decomp, count, pureDenoms).Select(Money.Satoshis).ToList();
				setCandidates.TryAdd(CalculateHash(currentSet), currentSet);
			}
		}

		return setCandidates;
	}

	private IDictionary<int, List<Money>> CreatePreDecompositions(List<Money> denoms, Money myInputSum, int maxNumberOfOutputsAllowed)
	{
		var setCandidates = new Dictionary<int, List<Money>>();
		int maxCount = Math.Min(maxNumberOfOutputsAllowed, AvailableVsize / MinimumOutputSize);

		for (int i = 0; i < 10_000; i++)
		{
			var remainingMoney = myInputSum;
			var remainingCount = maxCount;
			List<Money> currentSet = new();
			while (remainingCount > 0)
			{
				var denom = denoms.Where(x => IsDenomCanBeChoosen(x, remainingMoney, remainingCount) && x + MinimumOutputFee >= remainingMoney / 3).RandomElement(Random)
					?? denoms.FirstOrDefault(x => IsDenomCanBeChoosen(x, remainingMoney, remainingCount));

				if (denom is null)
				{
					break;
				}

				currentSet.Add(denom);
				remainingMoney -= denom + MinimumOutputFee;
				remainingCount--;
			}

			if (remainingMoney >= MinAllowedOutputAmount + MinimumOutputFee)
			{
				currentSet.Add(remainingMoney - MinimumOutputFee);
			}

			setCandidates.TryAdd(CalculateHash(currentSet), currentSet);
		}

		return setCandidates;
	}

	private KeyValuePair<int, List<Money>> CreateNaiveDecomposition(List<Money> denoms, Money myInputSum, int maxNumberOfOutputsAllowed)
	{
		List<Money> naiveSet = new();
		int maxCount = Math.Min(maxNumberOfOutputsAllowed, AvailableVsize / MinimumOutputSize);
		var remainingMoney = myInputSum;
		var remainingCount = maxCount;
		foreach (var denom in denoms)
		{
			while (IsDenomCanBeChoosen(denom, remainingMoney, remainingCount))
			{
				naiveSet.Add(denom);
				remainingMoney -= denom + MinimumOutputFee;
				remainingCount--;
			}
		}

		if (remainingMoney >= MinAllowedOutputAmount + MinimumOutputFee)
		{
			naiveSet.Add(remainingMoney - MinimumOutputFee);
		}

		// This can happen when smallest denom is larger than the input sum.
		if (naiveSet.Count == 0)
		{
			naiveSet.Add(remainingMoney - MinimumOutputFee);
		}

		return KeyValuePair.Create(CalculateHash(naiveSet), naiveSet);
	}

	private bool IsDenomCanBeChoosen(Money denom, Money remainingMoney, int remainingCount)
	{
		Money remainingAfterDenom = remainingMoney - denom - MinimumOutputFee;
		// Simple case, not enough resource
		if (remainingAfterDenom < Money.Zero || remainingCount <= 0)
		{
			return false;
		}

		// We have money above MinAllowedOutputAmount left
		if (remainingAfterDenom >= MinAllowedOutputAmount)
		{
			// There is no vsize/count for it, or with change it would be below MinAllowedOutputAmount
			if (remainingCount == 1 || remainingAfterDenom < MinAllowedOutputAmount + MinimumOutputFee)
			{
				return false;
			}
		}
		return true;
	}

	private Output[] CreateOutputs(List<Money> results, Money inputSum, List<Money> denoms)
	{
		Money resultsSum = results.Sum() + (MinimumOutputFee * results.Count);
		if (resultsSum > inputSum || results.Count == 0)
		{
			return Array.Empty<Output>();
		}

		// list with and without change
		bool withChange = !denoms.Contains(results[^1]);
		int moneyAvailable = (int)Math.Min(withChange ? results[^1].Satoshi - MinAllowedOutputAmount.Satoshi : inputSum.Satoshi - resultsSum.Satoshi, 10000);

		// we spend maximum half of the money that not yet spent to use other ScriptTypes
		long moneyToSpend = Random.GetInt(0, moneyAvailable / 2 + 1);
		int vsizeToSpend = AvailableVsize - results.Count * MinimumOutputSize;
		Dictionary<int, ScriptType> scriptTypes = new();
		for (int idx = 0; idx < 5; idx++)
		{
			ScriptType type = AllowedOutputTypes.RandomElement(Random);
			int vsize = type.EstimateOutputVsize();
			long priceDiff = FeeRate.GetFee(vsize) - MinimumOutputFee;
			int vsizeDiff = vsize - MinimumOutputSize;

			if (priceDiff <= moneyToSpend && vsizeDiff <= vsizeToSpend)
			{
				scriptTypes.AddOrReplace(Random.GetInt(0, results.Count), type);
				moneyToSpend -= priceDiff;
				vsizeToSpend -= vsizeDiff;
			}
		}

		var outputs = new Output[results.Count];
		for (int idx = 0; idx < outputs.Length; idx++)
		{
			outputs[idx] = Output.FromDenomination(results[idx], scriptTypes.GetValueOrDefault(idx, ScriptType.P2WPKH), FeeRate);
		}
		if (withChange)
		{
			var diff = inputSum - outputs.Sum(x => x.EffectiveCost);
			Output last = outputs[^1];
			outputs[^1] = Output.FromDenomination(last.Amount + diff, last.ScriptType, FeeRate);
		}
		return outputs;
	}

	private int CalculateHash(IEnumerable<Money> outputs)
	{
		HashCode hash = new();
		foreach (var item in outputs.OrderBy(x => x))
		{
			hash.Add(item);
		}
		return hash.ToHashCode();
	}
}
