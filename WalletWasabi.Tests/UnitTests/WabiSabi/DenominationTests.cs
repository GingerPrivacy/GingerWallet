using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Recommendation;

using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi;

public class DenominationTests
{
	[Theory]
	[InlineData(5000, 10000_0000)]
	public void StandardDenominationsTest(long minOutputAmount, long maxOutputAmount)
	{
		var denoms = DenominationFactory.CreateStandardDenominations(minOutputAmount, maxOutputAmount);

		// Unique, reverse ordered
		var cmp = denoms.ToHashSet().ToList();
		cmp.Sort((x, y) => y.CompareTo(x));
		Assert.True(denoms.SequenceEqual(cmp));

		// 2^n, 3^n, 2*3^n, 10^n, 2*10^n, 5*10^n
		long[] power3 = [1, 2];
		long[] power10 = [1, 2, 5];
		Assert.All(denoms, v => Assert.True(power10.Contains(SingleTag(v.Satoshi, 10)) || power3.Contains(SingleTag(v.Satoshi, 3)) || SingleTag(v.Satoshi, 2) == 1));

		// between minOutputAmount and maxOutputAmount
		Assert.True(minOutputAmount <= denoms[^1].Satoshi && maxOutputAmount >= denoms[0].Satoshi);
	}

	[Theory]
	[InlineData(9999, 5, 0.003, new double[] { 0.04194304, 0.03188646, 0.03188646, 0.01000000, 0.01000000, 0.00531441, 0.00531441, 0.00531441, 0.00531427, 0.00524288, 0.00524288, 0.00500000, 0.00262144, 0.00262144, 0.00262144, 0.00200000, 0.00200000, 0.00177147, 0.00177147, 0.00177147, 0.00131072, 0.00118098, 0.00100000, 0.00065536, 0.00039366, 0.00039366, 0.00039366, 0.00039366, 0.00032768, 0.00020000, 0.00016384, 0.00016384, 0.00016384, 0.00016384, 0.00010000, 0.00010000, 0.00010000 })]
	public void PreferedDenominationTests(long minOutputAmount, decimal miningFee, decimal coordinatorFee, double[] inputs)
	{
		FeeRate feeRate = new(miningFee);
		DenominationFactory denomFactory = new(minOutputAmount, 100_0000_0000);
		var effectiveInput = GetEffectiveMoney(inputs, miningFee, coordinatorFee);

		var denoms = denomFactory.CreatePreferedDenominations(effectiveInput, feeRate);

		// Unique, reverse ordered
		var cmp = denoms.ToHashSet().ToList();
		cmp.Sort((x, y) => y.CompareTo(x));
		Assert.True(denoms.SequenceEqual(cmp));

		// 2^n, 3^n, 2*3^n, 10^n, 2*10^n, 5*10^n
		long[] power3 = [1, 2];
		long[] power10 = [1, 2, 5];
		Assert.All(denoms, v => Assert.True(power10.Contains(SingleTag(v.Satoshi, 10)) || power3.Contains(SingleTag(v.Satoshi, 3)) || SingleTag(v.Satoshi, 2) == 1));

		// between minOutputAmount and
		Assert.True(minOutputAmount <= denoms[^1].Satoshi && effectiveInput.Max() >= denoms[0]);

		Assert.True(denomFactory.IsValidDenomination(denoms, effectiveInput, feeRate));
	}

	[Theory]
	[InlineData(9999, 5, 0.003, 1, new double[] { 0.04194304, 0.03188646, 0.03188646, 0.01000000, 0.01000000, 0.00531441, 0.00531441, 0.00531441, 0.00531427, 0.00524288, 0.00524288, 0.00500000, 0.00262144, 0.00262144, 0.00262144, 0.00200000, 0.00200000, 0.00177147, 0.00177147, 0.00177147, 0.00131072, 0.00118098, 0.00100000, 0.00065536, 0.00039366, 0.00039366, 0.00039366, 0.00039366, 0.00032768, 0.00020000, 0.00016384, 0.00016384, 0.00016384, 0.00016384, 0.00010000, 0.00010000, 0.00010000 })]
	public void AmountDecomposerTests(long minOutputAmount, decimal miningFee, decimal coordinatorFee, int maxOutputs, double[] inputs)
	{
		DeterministicRandom random = new(0x0900be81f6df6068);

		FeeRate feeRate = new(miningFee);
		DenominationFactory denomFactory = new(minOutputAmount, 100_0000_0000);
		var effectiveInput = GetEffectiveMoney(inputs, miningFee, coordinatorFee);
		var denoms = denomFactory.CreatePreferedDenominations(effectiveInput, feeRate);

		var allowedOutputTypes = new List<ScriptType>() { ScriptType.Taproot, ScriptType.P2WPKH };

		int outputSize = ScriptType.P2WPKH.EstimateOutputVsize();

		int withChange = 0, withoutChange = 0;
		for (int inputNum = 5; inputNum < Math.Min(20, inputs.Length / 2); inputNum++)
		{
			var decomposer = new AmountDecomposer(feeRate, minOutputAmount, 100_0000_0000, inputNum * maxOutputs * outputSize, allowedOutputTypes, new DeterministicRandom(0x4886be225bd42405L));
			for (int tests = 0; tests < Math.Min(inputNum + 2, 10); tests++)
			{
				effectiveInput.Shuffle(random);
				var myInputs = effectiveInput.Take(inputNum).ToList();
				var myInputsSum = myInputs.Sum();

				var decompose = decomposer.Decompose(myInputsSum, denoms);
				var decomposeSum = decompose.Sum(x => x.EffectiveCost);
				if (denomFactory.StandardDenominations.Contains(decompose.Last().Amount))
				{
					withoutChange++;
				}
				else
				{
					withChange++;
				}
				Assert.True(denomFactory.StandardDenominations.Contains(decompose.Last().Amount) || decomposeSum == myInputsSum);
				Assert.True(decomposeSum <= myInputsSum && decomposeSum + minOutputAmount > myInputsSum);
			}
		}
		// We shoud have tests for both cases
		Assert.True(withChange > 0 && withoutChange > 0);
	}

	[Fact]
	public void ValidationTest()
	{
		double[] inputs = [0.00121826, 0.00523669, 0.00040754, 0.00490754, 0.09529469, 0.00345048, 0.01050448, 0.00523669, 0.00523669, 0.00254372];
		double[] denoms = [0.01048576, 0.00500000, 0.00039366, 0.00013122];

		var inputEffectiveValues = inputs.Select(x => new Money((decimal)x, MoneyUnit.BTC)).ToList();
		var denomsMoney = denoms.Select(x => new Money((decimal)x, MoneyUnit.BTC)).ToList();

		FeeRate miningFee = new((decimal)134);
		DenominationFactory denominationFactory = new(5000L, 10_0000_0000L);
		var denoms2 = denominationFactory.CreatePreferedDenominations(inputEffectiveValues, miningFee);

		Assert.True(denominationFactory.IsValidDenomination(denomsMoney, inputEffectiveValues, miningFee));
		Assert.True(denominationFactory.IsValidDenomination(denoms2, inputEffectiveValues, miningFee));
	}

	protected long SingleTag(long value, long power)
	{
		while (value != 0 && (value % power) == 0)
		{
			value /= power;
		}
		return value;
	}

	private List<Money> GetEffectiveMoney(double[] list, decimal miningFee = 0, decimal coinjoinFee = 0)
	{
		FeeRate feeRate = new(miningFee);
		CoordinationFeeRate coordinationFee = new(coinjoinFee, new(0.01m, MoneyUnit.BTC));
		var inputFee = feeRate.GetFeeWithZero(ScriptType.P2WPKH.EstimateInputVsize());
		return list.Select(x =>
		{
			var m = new Money((decimal)x, MoneyUnit.BTC);
			return m - coordinationFee.GetFee(m) - inputFee;
		}).ToList();
	}
}
