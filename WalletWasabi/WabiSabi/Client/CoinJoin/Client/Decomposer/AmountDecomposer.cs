using GingerCommon.Static;
using LinqKit;
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
	public AmountDecomposer(FeeRate feeRate, Money minAllowedOutputAmount, Money maxAllowedOutputAmount, int availableVsize, IList<ScriptType> allowedOutputTypes, WasabiRandom random)
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
	public IList<ScriptType> AllowedOutputTypes { get; }
	public Money MinAllowedOutputAmount { get; }
	public Money MaxAllowedOutputAmount { get; }

	private WasabiRandom Random { get; }

	public IEnumerable<Output> Decompose(Money myInputSum, List<Money> denoms, List<double>? freqs = null, int features = 8)
	{
		var maxNumberOfOutputsAllowed = Math.Min(MaximumOutputNumber, 10);
		var maxNumberOfOutputsAllowedForChangeless = Math.Min(MaximumOutputNumber, Math.Max(maxNumberOfOutputsAllowed, 12));

		// If there are no output denominations, the participation in coinjoin makes no sense.
		if (denoms.Count == 0)
		{
			throw new InvalidOperationException(
				"No valid output denominations found. This can occur when an insufficient number of coins are registered to participate in the coinjoin.");
		}
		// If my input sum is smaller than the smallest denomination, then participation in a coinjoin makes no sense.
		if (denoms[^1] + MinimumOutputFee > myInputSum)
		{
			throw new InvalidOperationException("Not enough coins registered to participate in the coinjoin.");
		}

		// Create many decompositions for optimization.
		var setCandidates = new Dictionary<int, List<Money>>();
		// Try changless first, if we have, we won't bother with the others
		AddChangelessDecompositions(setCandidates, denoms, myInputSum, maxNumberOfOutputsAllowed, maxNumberOfOutputsAllowedForChangeless);
		var changless = setCandidates.Count != 0;
		if (!changless)
		{
			// Create the most naive decomposition for starter.
			AddNaiveDecomposition(setCandidates, denoms, myInputSum, maxNumberOfOutputsAllowed);
			// Create more pre-decompositions for sanity.
			AddPreDecompositions(setCandidates, denoms, myInputSum, maxNumberOfOutputsAllowed);
		}

		FilterDecompositions(setCandidates, denoms);

		// Create Outputs from it
		var outputCandidates = setCandidates.Select(x =>
		{
			var output = CreateOutputs(x.Value, myInputSum, denoms);
			var cost = myInputSum - output.Sum(x => x.Amount - x.InputFee);
			return (Decomposition: output, Cost: cost);
		}).Where(x => x.Decomposition.Length > 0).ToList();
		outputCandidates.Shuffle(Random);

		var finalCandidate = (features & 8) == 0 ? ChooseCandidateDeprecated(myInputSum, denoms, changless, outputCandidates) : ChooseCandidate(myInputSum, denoms, freqs, outputCandidates);

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

	private Output[] ChooseCandidate(Money myInputSum, List<Money> denoms, List<double>? frequencies, List<(Output[] Decomposition, Money Cost)> outputCandidates)
	{
		Dictionary<Money, int> groupCount = new();
		Dictionary<Money, double> denomFrequency = new();
		double defaultFreq = 1.0 * myInputSum.Satoshi / (denoms.Sum().Satoshi + denoms.Count * MinimumOutputFee.Satoshi);
		for (int idx = 0; idx < denoms.Count; idx++)
		{
			denomFrequency.Add(denoms[idx], frequencies is not null && idx < frequencies.Count ? Math.Max(frequencies[idx], 1.0) : defaultFreq);
		}

		var evaluatedCandidates = outputCandidates.Select(x =>
		{
			var costRate = 1.0 / Math.Max(x.Cost.Satoshi, 100);
			groupCount.Clear();
			x.Decomposition.ForEach(x => groupCount.AddValue(x.Amount, 1));
			// This way the change value will get extra penalty
			var denomRate = groupCount.Sum(x => x.Key.Satoshi * (denomFrequency.GetValueOrDefault(x.Key, 0.0) - x.Value));
			return (Goodness: denomRate * costRate, x.Decomposition);
		}).OrderByDescending(x => x.Goodness).ToList();

		var goodnessLimit = (evaluatedCandidates[0].Goodness > 0 ? 0.9 : 1 / 0.9) * evaluatedCandidates[0].Goodness;
		var finalCandidates = evaluatedCandidates.Where(x => x.Goodness >= goodnessLimit).ToList();

		var finalCandidate = finalCandidates.RandomElement(Random).Decomposition;
		return finalCandidate;
	}

	private Output[] ChooseCandidateDeprecated(Money myInputSum, List<Money> denoms, bool changless, List<(Output[] Decomposition, Money Cost)> outputCandidates)
	{
		var denomHashSet = denoms.ToHashSet();
		var orderedCandidates = outputCandidates
			.OrderBy(x => GetChange(x.Decomposition, denomHashSet)) // Less change is better.
			.ThenBy(x => x.Cost) // Less cost is better.
			.ThenBy(x => x.Decomposition.Any(d => d.ScriptType == ScriptType.Taproot) && x.Decomposition.Any(d => d.ScriptType == ScriptType.P2WPKH) ? 0 : 1) // Prefer mixed scripts types.
			.Select(x => x).ToList();

		// We want to introduce randomness between the best selections.
		// If we successfully avoided change, then what matters is cost,
		// if we didn't then cost calculation is irrelevant, because the size of change is more costly.
		(Output[] Decomp, Money Cost)[] finalCandidates;
		if (changless)
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

		return finalCandidate;
	}

	// The change is always the last element
	private static Money GetChange(Output[] decomposition, HashSet<Money> denomHashSet)
	{
		return decomposition.Length == 0 || denomHashSet.Contains(decomposition[^1].Amount) ? Money.Zero : decomposition[^1].Amount;
	}

	private void FilterDecompositions(Dictionary<int, List<Money>> candidateList, List<Money> denoms)
	{
		if (candidateList.Count == 0)
		{
			return;
		}
		var candidate = candidateList.Values.First();
		if (denoms.Contains(candidate[^1]))
		{
			// Changeless solutions
			return;
		}

		// Candidate list with change
		var maxDenom = denoms[0];
		var highChanges = candidateList.Where(x => x.Value[^1] > maxDenom).Select(x => x.Key).ToList();
		if (highChanges.Count > 0 && highChanges.Count < candidateList.Count)
		{
			highChanges.ForEach(x => candidateList.Remove(x));
		}
	}

	private void AddChangelessDecompositions(Dictionary<int, List<Money>> candidateList, List<Money> denoms, Money myInputSum, int preferedNumberOfOutputsAllowed, int maxNumberOfOutputsAllowed)
	{
		int origCount = candidateList.Count;
		AddChangelessDecompositions(candidateList, denoms, myInputSum, preferedNumberOfOutputsAllowed);
		if (candidateList.Count == origCount && preferedNumberOfOutputsAllowed < maxNumberOfOutputsAllowed)
		{
			AddChangelessDecompositions(candidateList, denoms, myInputSum, maxNumberOfOutputsAllowed);
		}
	}

	private void AddChangelessDecompositions(Dictionary<int, List<Money>> candidateList, List<Money> denoms, Money myInputSum, int maxNumberOfOutputsAllowed)
	{
		if (maxNumberOfOutputsAllowed > 1)
		{
			var pureDenoms = denoms.Select(x => x.Satoshi).ToArray();
			var decompDenoms = denoms.Select(x => x.Satoshi + MinimumOutputFee.Satoshi).ToArray();
			foreach (var (sum, count, decomp) in Decomposer.Decompose((long)myInputSum, MinAllowedOutputAmount, Math.Min(maxNumberOfOutputsAllowed, 12), decompDenoms))
			{
				var currentSet = Decomposer.ToRealValuesArray(decomp, count, pureDenoms).Select(Money.Satoshis).ToList();
				candidateList.TryAdd(CalculateHash(currentSet), currentSet);
			}
		}
	}

	private void AddPreDecompositions(Dictionary<int, List<Money>> candidateList, List<Money> denoms, Money myInputSum, int maxNumberOfOutputsAllowed)
	{
		int maxCount = Math.Min(maxNumberOfOutputsAllowed, AvailableVsize / MinimumOutputSize);

		for (int i = 0; i < 10_000; i++)
		{
			var remainingMoney = myInputSum;
			var remainingCount = maxCount;
			List<Money> currentSet = new();
			while (remainingCount > 0)
			{
				var denomList = denoms.Where(x => IsDenomCanBeChoosen(x, remainingMoney, remainingCount) && x + MinimumOutputFee >= remainingMoney / 8).ToList();
				var denom = denomList.RandomElement(Random) ?? denoms.FirstOrDefault(x => IsDenomCanBeChoosen(x, remainingMoney, remainingCount));

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

			candidateList.TryAdd(CalculateHash(currentSet), currentSet);
		}
	}

	private void AddNaiveDecomposition(Dictionary<int, List<Money>> candidateList, List<Money> denoms, Money myInputSum, int maxNumberOfOutputsAllowed)
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

		candidateList.TryAdd(CalculateHash(naiveSet), naiveSet);
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

		Dictionary<int, ScriptType> scriptTypes = new();
		if (AllowedOutputTypes.Count > 1)
		{
			// we spend maximum half of the money that not yet spent to use other ScriptTypes
			int moneyAvailable = (int)Math.Min(withChange ? results[^1].Satoshi - MinAllowedOutputAmount.Satoshi : inputSum.Satoshi - resultsSum.Satoshi, 10000);
			long moneyToSpend = Random.GetInt(0, moneyAvailable / 2 + 1);
			int vsizeToSpend = AvailableVsize - results.Count * MinimumOutputSize;
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

	private int CalculateHash(List<Money> outputs)
	{
		HashCode hash = new();
		foreach (var item in outputs.OrderBy(x => x))
		{
			hash.Add(item);
		}
		return hash.ToHashCode();
	}
}
