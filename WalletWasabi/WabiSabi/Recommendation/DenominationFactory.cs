using GingerCommon.Static;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Recommendation;

public class DenominationFactory
{
	public DenominationFactory(Money minAllowedOutputAmount, Money maxAllowedOutputAmount)
	{
		MinAllowedOutputAmount = minAllowedOutputAmount;
		MaxAllowedOutputAmount = maxAllowedOutputAmount;

		StandardDenominations = CreateStandardDenominations(MinAllowedOutputAmount, MaxAllowedOutputAmount);
	}

	public Money MinAllowedOutputAmount { get; }
	public Money MaxAllowedOutputAmount { get; }

	public List<Money> StandardDenominations { get; }

	public virtual List<double> CreateDenominationFrequencies(IList<Money> inputEffectiveValues, FeeRate miningFee, List<Money> denoms)
	{
		double freq = 1.0 * inputEffectiveValues.Sum().Satoshi / (denoms.Sum().Satoshi + denoms.Count * miningFee.GetFee(ScriptType.P2WPKH.EstimateOutputVsize()).Satoshi);
		return denoms.Select(x => freq).ToList();
	}

	public virtual List<Money> CreatePreferedDenominations(IList<Money> inputEffectiveValues, FeeRate miningFee)
	{
		return CreateDefaultDenominations(inputEffectiveValues, miningFee);
	}

	public List<Money> CreateDefaultDenominations(IList<Money> inputEffectiveValues, FeeRate miningFee)
	{
		var histogram = GetDenominationFrequencies(inputEffectiveValues, miningFee);

		// Filter out and order denominations those have occurred in the frequency table at least twice.
		var preFilteredDenoms = histogram
			.Where(x => x.Value > 1)
			.OrderByDescending(x => x.Key)
			.Select(x => x.Key)
			.ToArray();

		// Filter out denominations very close to each other.
		// Heavy filtering on the top, little to no filtering on the bottom,
		// because in smaller denom levels larger users are expected to participate,
		// but on larger denom levels there's little chance of finding each other.
		var increment = 0.5 / preFilteredDenoms.Length;
		List<Money> denoms = new();
		var currentLength = preFilteredDenoms.Length;
		foreach (var denom in preFilteredDenoms)
		{
			var filterSeverity = 1 + currentLength * increment;
			if (denoms.Count == 0 || denom.Satoshi <= (long)(denoms.Last().Satoshi / filterSeverity))
			{
				denoms.Add(denom);
			}
			currentLength--;
		}

		return denoms;
	}

	public bool IsValidDenomination(IList<Money> denoms, IList<Money> inputEffectiveValues, FeeRate miningFee, IList<Money>? defaultDenominations = null)
	{
		if (denoms.Count == 0 || inputEffectiveValues.Count == 0)
		{
			return false;
		}

		// Should be reverse ordered, unique, use standard denomination levels
		for (int idx = 0, len = denoms.Count - 1; idx < len; idx++)
		{
			if (denoms[idx] <= denoms[idx + 1] || !StandardDenominations.Contains(denoms[idx]))
			{
				return false;
			}
		}

		// Last elem also should use standard denomination levels
		if (!StandardDenominations.Contains(denoms[^1]))
		{
			return false;
		}

		var maxInput = inputEffectiveValues.Max();
		// Now we allow denom above the biggest input to fight against the coin fragmentation
		// There is no garantee that denoms[^1] <= inputEffectiveValues.Min(), that's completely valid!
		if (denoms[0] > 3 * maxInput || denoms[^1] < MinAllowedOutputAmount)
		{
			return false;
		}

		// The default denomination list is valid by definition, we use that when in doubt
		defaultDenominations ??= CreateDefaultDenominations(inputEffectiveValues, miningFee);
		var minimumOutputFee = miningFee.GetFee(ScriptType.P2WPKH.EstimateOutputVsize()).Satoshi;

		// the last member should be small enough
		if (defaultDenominations.Count > 0 && denoms[^1] > defaultDenominations[^1])
		{
			// We might allow this, but all input should be able to work that worked with the default
			var smallestSingle = inputEffectiveValues.Where(x => x - minimumOutputFee >= defaultDenominations[^1]).Min();
			if (smallestSingle - minimumOutputFee < denoms[^1])
			{
				return false;
			}
		}

		// We shouldn't be too far from the next
		var secondInput = inputEffectiveValues.Where(x => x < maxInput).Max() ?? maxInput;
		for (int idx = 0, len = denoms.Count - 1; idx < len; idx++)
		{
			if (denoms[idx] < secondInput && denoms[idx].Satoshi > 6.2 * (denoms[idx + 1].Satoshi + minimumOutputFee))
			{
				int dIdx = defaultDenominations.IndexOf(denoms[idx]);
				if (dIdx < 0 || dIdx + 1 >= defaultDenominations.Count || defaultDenominations[dIdx + 1] != denoms[idx + 1])
				{
					return false;
				}
			}
		}

		return true;
	}

	/// <returns>Pair of denomination and the number of times we found it in a breakdown.</returns>
	public SortedDictionary<Money, int> GetDenominationFrequencies(IEnumerable<Money> inputEffectiveValues, FeeRate miningFee)
	{
		var minimumOutputFee = miningFee.GetFee(ScriptType.P2WPKH.EstimateOutputVsize());
		// We can't change this function significantly as the coinjoin heavily based on this function's deterministic nature:
		// each client gets about same result of the denominations from the input list

		SortedDictionary<Money, int> inputs = new();
		foreach (var input in inputEffectiveValues)
		{
			inputs.AddValue(input, 1);
		}

		// the highest output is allowed only we have enough of them
		var outputLimit = inputs.Last().Value > 1 ? inputs.Last().Key : inputs.SkipLast(1).Last().Key;
		var denomsForBreakDown = StandardDenominations.Where(x => x <= outputLimit); // Take only affordable denominations.

		SortedDictionary<Money, int> denomFrequencies = new();
		foreach (var input in inputs)
		{
			Money amount = input.Key;
			Money? denom = null;
			while ((denom = denomsForBreakDown.FirstOrDefault(x => x + minimumOutputFee <= amount)) != null)
			{
				denomFrequencies.AddValue(denom, input.Value);
				amount -= denom + minimumOutputFee;
			}
		}

		return denomFrequencies;
	}

	private static void AddDenominations(List<Money> dest, Money minAllowedOutputAmount, Money maxAllowedOutputAmount, Func<int, double> generator)
	{
		Money amount;
		for (int i = 0; i < int.MaxValue && (amount = Money.Satoshis((ulong)generator(i))) <= maxAllowedOutputAmount; i++)
		{
			if (amount >= minAllowedOutputAmount)
			{
				dest.Add(amount);
			}
		}
	}

	public static List<Money> CreateStandardDenominations(Money minAllowedOutputAmount, Money maxAllowedOutputAmount)
	{
		List<Money> result = new();
		AddDenominations(result, minAllowedOutputAmount, maxAllowedOutputAmount, i => Math.Pow(2, i));
		AddDenominations(result, minAllowedOutputAmount, maxAllowedOutputAmount, i => Math.Pow(3, i));
		AddDenominations(result, minAllowedOutputAmount, maxAllowedOutputAmount, i => 2 * Math.Pow(3, i));
		AddDenominations(result, minAllowedOutputAmount, maxAllowedOutputAmount, i => Math.Pow(10, i));
		AddDenominations(result, minAllowedOutputAmount, maxAllowedOutputAmount, i => 2 * Math.Pow(10, i));
		AddDenominations(result, minAllowedOutputAmount, maxAllowedOutputAmount, i => 5 * Math.Pow(10, i));

		result.Sort((x, y) => y.CompareTo(x));
		return result;
	}
}
