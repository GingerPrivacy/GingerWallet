using GingerCommon.Crypto.Random;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.TransactionBuilding;

public class ChangelessTransactionCoinSelectorTests
{
	private static readonly HdPubKey[] Keys = CreateKeys();
	private readonly GingerRandom _random = TestRandom.Get();

	[Theory]
	[InlineData(150_000, true)]
	[InlineData(65_000, false)]
	public async Task FindsKnownSuggestionsAsync(long target, bool more)
	{
		long[] amounts = { 6_025, 6_561, 8_192, 13_122, 50_000, 100_000, 196_939, 524_288 };
		var coins = amounts.Select((amount, index) => Coin(index, amount)).ToArray();
		FeeRate feeRate = new(2m);
		var suggestions = await GetSuggestionsAsync(coins, feeRate, OutputWithTarget(target, feeRate), coins.Length);

		Assert.Equal(2, suggestions.Length);
		long[] expected = more ? new long[] { 100_000, 50_000, 6_025 } : new long[] { 50_000, 8_192, 6_561 };
		Assert.Equal(expected, suggestions[more ? 0 : 1].Select(coin => coin.Amount.Satoshi).OrderByDescending(amount => amount));
	}

	[Fact]
	public async Task MatchesExhaustiveGroupedSelectionAsync()
	{
		Random random = new(712_389);
		FeeRate[] feeRates = { new(0.1m), new(1.2m), new(25m) };
		for (int iteration = 0; iteration < 150; iteration++)
		{
			FeeRate feeRate = feeRates[iteration % feeRates.Length];
			int groupCount = random.Next(1, Keys.Length + 1);
			List<SmartCoin> coins = new();
			for (int group = 0; group < groupCount; group++)
			{
				int coinCount = random.Next(1, 4);
				for (int coin = 0; coin < coinCount; coin++)
				{
					coins.Add(Coin(group, random.Next(1, 50_001)));
				}
			}

			long target = random.Next(2_001, (int)coins.Sum(coin => coin.Amount.Satoshi) + 10_000);
			int maxInputCount = random.Next(groupCount + 1);
			var expected = ExhaustiveSelections(coins, feeRate, target, maxInputCount);
			var actual = await GetSuggestionsAsync(coins, feeRate, OutputWithTarget(target, feeRate), maxInputCount);
			var objectives = actual.Select(selected => (
				Value: selected.Sum(coin => coin.EffectiveValue(feeRate).Satoshi),
				Cost: selected.Sum(coin => InputFee(coin.ScriptPubKey, feeRate)))).ToArray();

			Assert.Equal(expected, objectives);
			foreach (var selected in actual)
			{
				Assert.Equal(selected.Count, selected.Distinct().Count());
				Assert.InRange(selected.Select(coin => coin.ScriptPubKey).Distinct().Count(), 1, maxInputCount);
				Assert.All(selected.GroupBy(coin => coin.ScriptPubKey), group =>
					Assert.Equal(coins.Count(coin => coin.ScriptPubKey == group.Key), group.Count()));
			}
		}
	}

	[Fact]
	public async Task EqualEffectiveValuesSelectCheaperGroupAsync()
	{
		FeeRate feeRate = new(1.2m);
		long fee = InputFee(Keys[0].P2wpkhScript, feeRate);
		SmartCoin cheaper = Coin(0, 10_000 + fee);
		SmartCoin[] coins = { Coin(1, 5_000 + fee), Coin(1, 5_000 + fee), cheaper };

		var suggestions = await GetSuggestionsAsync(coins, feeRate, OutputWithTarget(10_000, feeRate), 2);

		Assert.Equal(2, suggestions.Length);
		Assert.All(suggestions, selected => Assert.Same(cheaper, Assert.Single(selected)));
	}

	[Theory]
	[InlineData(9_999, 11_200)]
	[InlineData(8_800, 10_001)]
	public async Task KeepsBothSuggestionsWhenTheirTargetDistancesDifferAsync(long below, long above)
	{
		FeeRate feeRate = new(1.2m);
		SmartCoin less = Coin(0, below + InputFee(Keys[0].P2wpkhScript, feeRate));
		SmartCoin more = Coin(1, above + InputFee(Keys[1].P2wpkhScript, feeRate));

		var suggestions = await GetSuggestionsAsync(new[] { less, more }, feeRate, OutputWithTarget(10_000, feeRate), 1);

		Assert.Equal(2, suggestions.Length);
		Assert.Same(more, Assert.Single(suggestions[0]));
		Assert.Same(less, Assert.Single(suggestions[1]));
	}

	[Theory]
	[InlineData(74, 0)]
	[InlineData(75, 1)]
	[InlineData(101, 2)]
	[InlineData(126, 1)]
	[InlineData(127, 0)]
	public async Task PaymentBoundsAreInclusiveAndTruncatedAsync(long amount, int expectedCount)
	{
		FeeRate feeRate = FeeRate.Zero;
		SmartCoin coin = Coin(0, amount + InputFee(Keys[0].P2wpkhScript, feeRate));

		var suggestions = await GetSuggestionsAsync(new[] { coin }, feeRate, OutputWithTarget(101, feeRate), 1);

		Assert.Equal(expectedCount, suggestions.Length);
		Assert.All(suggestions, selected => Assert.Same(coin, Assert.Single(selected)));
	}

	[Fact]
	public async Task KeepsScriptGroupsTogetherAcrossSuggestionsAsync()
	{
		SmartCoin[] first = { Coin(0, 10_000), Coin(0, 10_000), Coin(0, 10_000) };
		SmartCoin[] second = { Coin(1, 10_000), Coin(1, 10_000), Coin(1, 10_000), Coin(1, 5_000) };
		var coins = first.Concat(second).ToArray();
		FeeRate feeRate = FeeRate.Zero;

		var suggestions = await GetSuggestionsAsync(coins, feeRate, OutputWithTarget(30_000, feeRate), 1);

		Assert.Equal(2, suggestions.Length);
		Assert.Equal(second, suggestions[0]);
		Assert.Equal(first, suggestions[1]);

		var combined = Assert.Single(await GetSuggestionsAsync(coins, feeRate, OutputWithTarget(60_000, feeRate), 2));
		Assert.Equal(coins.Length, combined.Count);
		Assert.All(coins, coin => Assert.Contains(coin, combined));
	}

	[Theory]
	[InlineData(0.1)]
	[InlineData(1.2)]
	[InlineData(25)]
	public async Task IncludesUneconomicalMembersOfPositiveGroupsAsync(decimal satoshisPerVbyte)
	{
		FeeRate feeRate = new(satoshisPerVbyte);
		SmartCoin[] group = { Coin(0, 10_000), Coin(0, 1) };
		SmartCoin[] otherCoins = { Coin(1, 1), Coin(2, InputFee(Keys[2].P2wpkhScript, feeRate)) };
		var coins = group.Concat(otherCoins).ToArray();
		long target = group.Sum(coin => coin.EffectiveValue(feeRate).Satoshi);
		TxOut output = OutputWithTarget(target, feeRate);

		var suggestions = await GetSuggestionsAsync(coins, feeRate, output, 1);

		Assert.Equal(2, suggestions.Length);
		Assert.All(suggestions, selected => Assert.Equal(group, selected));
		Assert.Empty(await GetSuggestionsAsync(coins, feeRate, output, 0));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task EmptyOrNonpositiveGroupsProduceNoSuggestionsAsync(bool empty)
	{
		FeeRate feeRate = new(2m);
		SmartCoin[] coins = empty ? Array.Empty<SmartCoin>() : new[]
		{
			Coin(0, 1),
			Coin(1, InputFee(Keys[1].P2wpkhScript, feeRate))
		};

		Assert.Empty(await GetSuggestionsAsync(coins, feeRate, OutputWithTarget(10_000, feeRate), 2));
	}

	[Fact]
	public async Task PreCancelledRequestThrowsAsync()
	{
		SmartCoin[] coins = { Coin(0, 10_000) };
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			GetSuggestionsAsync(coins, FeeRate.Zero, OutputWithTarget(10_000, FeeRate.Zero), 1, new CancellationToken(canceled: true)));
	}

	private static (long Value, long Cost)[] ExhaustiveSelections(IReadOnlyList<SmartCoin> coins, FeeRate feeRate, long target, int maxInputCount)
	{
		var groups = coins.GroupBy(coin => coin.ScriptPubKey)
			.Select(group => (
				Value: group.Sum(coin => coin.EffectiveValue(feeRate).Satoshi),
				Cost: group.Sum(coin => InputFee(coin.ScriptPubKey, feeRate))))
			.Where(group => group.Value > 0).ToArray();
		List<(long Value, long Cost)> selections = new();
		for (int mask = 1; mask < (1 << groups.Length); mask++)
		{
			long value = 0;
			long cost = 0;
			int count = 0;
			for (int index = 0; index < groups.Length; index++)
			{
				if ((mask & (1 << index)) != 0)
				{
					value += groups[index].Value;
					cost += groups[index].Cost;
					count++;
				}
			}
			if (count <= maxInputCount)
			{
				selections.Add((value, cost));
			}
		}

		var more = selections.Where(selection => selection.Value >= target && selection.Value <= (long)(target * 1.25))
			.OrderBy(selection => selection.Value).ThenBy(selection => selection.Cost).Take(1);
		var less = selections.Where(selection => selection.Value <= target && selection.Value >= (long)(target * 0.75))
			.OrderByDescending(selection => selection.Value).ThenBy(selection => selection.Cost).Take(1);
		return more.Concat(less).ToArray();
	}

	private static async Task<IReadOnlyList<SmartCoin>[]> GetSuggestionsAsync(
		IReadOnlyList<SmartCoin> coins, FeeRate feeRate, TxOut output, int maxInputCount, CancellationToken cancellationToken = default)
	{
		List<IReadOnlyList<SmartCoin>> suggestions = new();
		await foreach (var selected in ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(coins, feeRate, output, maxInputCount, cancellationToken))
		{
			suggestions.Add(selected);
		}
		return suggestions.ToArray();
	}

	private static TxOut OutputWithTarget(long target, FeeRate feeRate)
	{
		Script script = Keys[0].P2wpkhScript;
		return new TxOut(Money.Satoshis(target) - feeRate.GetFee(script.EstimateOutputVsize()), script);
	}

	private static long InputFee(Script script, FeeRate feeRate) =>
		feeRate.GetFee(script.EstimateInputVsize()).Satoshi;

	private SmartCoin Coin(int key, long satoshis) =>
		BitcoinFactory.CreateSmartCoin(_random, Keys[key], Money.Satoshis(satoshis));

	private static HdPubKey[] CreateKeys()
	{
		KeyManager manager = ServiceFactory.CreateKeyManager();
		return Enumerable.Range(0, 12).Select(_ => BitcoinFactory.CreateHdPubKey(manager)).ToArray();
	}
}
