using GingerCommon.Crypto.Random;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionBuilding.BnB;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.TransactionBuilding;

/// <summary>
/// Tests for <see cref="ChangelessTransactionCoinSelector"/> class.
/// </summary>
public class ChangelessTransactionCoinSelectorTests
{
	/// <summary>
	/// Tests that we select coins so that user can agree with paying more money for having a payment transaction with no change output.
	/// </summary>
	[Fact]
	public void GoodSuggestion()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(1));

		KeyManager km = ServiceFactory.CreateKeyManager();
		IEnumerable<SmartCoin> coins = GenerateDummySmartCoins(TestRandom.Get(), km, 6_025, 6_561, 8_192, 13_122, 50_000, 100_000, 196_939, 524_288);

		var coinsByScript = coins
			.GroupBy(coin => coin.ScriptPubKey)
			.OrderByDescending(group => group.Sum(coin => coin.Amount))
			.ToList();

		Money target = Money.Satoshis(150_000);
		FeeRate feeRate = new(Money.Satoshis(2));

		long[] inputCosts = coinsByScript.Select(group => group.Sum(coin => feeRate.GetFee(coin.ScriptPubKey.EstimateInputVsize()).Satoshi)).ToArray();

		Dictionary<SmartCoin[], long> inputEffectiveValues = new(coinsByScript.ToDictionary(x => x.Select(coin => coin).ToArray(), x => x.Sum(coin => coin.EffectiveValue(feeRate).Satoshi)));
		StrategyParameters parameters = new(target, inputEffectiveValues.Values.ToArray(), inputCosts);
		MoreSelectionStrategy strategy = new(parameters);

		bool found = ChangelessTransactionCoinSelector.TryGetCoins(strategy, inputEffectiveValues, out var selectedCoins, testDeadlineCts.Token);
		Assert.True(found);

		long[] solution = selectedCoins!.Select(x => x.Amount.Satoshi).ToArray();
		Assert.Equal(new long[] { 100_000, 50_000, 6_025 }, solution);
		Assert.Equal(Money.Satoshis(6025), solution.Sum() - target); // ~4% more for the privacy.
	}

	[Fact]
	public void Good_LesserSuggestion()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(1));

		KeyManager km = ServiceFactory.CreateKeyManager();
		List<SmartCoin> coins = GenerateDummySmartCoins(TestRandom.Get(), km, 6_025, 6_561, 8_192, 13_122, 50_000, 100_000, 196_939, 524_288);

		var coinsByScript = coins
			.GroupBy(coin => coin.ScriptPubKey)
			.OrderByDescending(group => group.Sum(coin => coin.Amount))
			.ToList();

		Money target = Money.Satoshis(65_000);
		FeeRate feeRate = new(Money.Satoshis(2));

		long[] inputCosts = coinsByScript.Select(group => group.Sum(coin => feeRate.GetFee(coin.ScriptPubKey.EstimateInputVsize()).Satoshi)).ToArray();

		Dictionary<SmartCoin[], long> inputEffectiveValues = new(coinsByScript.ToDictionary(x => x.Select(coin => coin).ToArray(), x => x.Sum(coin => coin.EffectiveValue(feeRate).Satoshi)));
		StrategyParameters parameters = new(target, inputEffectiveValues.Values.ToArray(), inputCosts);
		LessSelectionStrategy strategy = new(parameters);

		bool found = ChangelessTransactionCoinSelector.TryGetCoins(strategy, inputEffectiveValues, out var selectedCoins, testDeadlineCts.Token);
		Assert.True(found);

		long[] solution = selectedCoins!.Select(x => x.Amount.Satoshi).ToArray();
		Assert.Equal(new long[] { 50_000, 8_192, 6_561 }, solution);
	}

	/// <summary>
	/// Tests that solutions respect <see cref="MoreSelectionStrategy.MaxExtraPayment"/> restriction.
	/// </summary>
	[Fact]
	public void TooExpensiveSolution()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(1));

		KeyManager km = ServiceFactory.CreateKeyManager();
		List<SmartCoin> coins = GenerateDummySmartCoins(TestRandom.Get(), km, 150_000);

		var coinsByScript = coins
			.GroupBy(coin => coin.ScriptPubKey)
			.OrderByDescending(group => group.Sum(coin => coin.Amount))
			.ToList();

		Money target = Money.Satoshis(100_000);
		FeeRate feeRate = new(Money.Satoshis(2));

		long[] inputCosts = coins.Select(x => feeRate.GetFee(x.ScriptPubKey.EstimateInputVsize()).Satoshi).ToArray();

		Dictionary<SmartCoin[], long> inputEffectiveValues = new(coinsByScript.ToDictionary(x => x.Select(coin => coin).ToArray(), x => x.Sum(coin => coin.EffectiveValue(feeRate).Satoshi)));
		StrategyParameters parameters = new(target, coins.Select(coin => coin.Amount.Satoshi).ToArray(), inputCosts);
		LessSelectionStrategy strategy = new(parameters);

		bool found = ChangelessTransactionCoinSelector.TryGetCoins(strategy, inputEffectiveValues, out var selectedCoins, testDeadlineCts.Token);
		Assert.False(found);
	}

	/// <summary>
	/// BnB algorithm chooses coins in the way that either all coins with the same scriptPubKey are selected or no coin with that scriptPubKey is selected.
	/// </summary>
	[Fact]
	public async Task BnBRespectScriptPubKeyPrivacyRuleAsync()
	{
		var rnd = TestRandom.Get();
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(5));

		KeyManager km = ServiceFactory.CreateKeyManager();
		HdPubKey constantHdPubKey = BitcoinFactory.CreateHdPubKey(km);
		HdPubKey constantHdPubKey2 = BitcoinFactory.CreateHdPubKey(km);

		// Coins with the same ScriptPubKey should be considered one coin, thus be chosen together.
		List<SmartCoin> availableCoins = new()
		{
			// Group #1.
			BitcoinFactory.CreateSmartCoin(rnd, constantHdPubKey, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(rnd, constantHdPubKey, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(rnd, constantHdPubKey, Money.Satoshis(10000)),

			// Group #2.
			BitcoinFactory.CreateSmartCoin(rnd, constantHdPubKey2, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(rnd, constantHdPubKey2, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(rnd, constantHdPubKey2, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(rnd, constantHdPubKey2, Money.Satoshis(5000)),
		};

		// First test case where we show that we are not mixing the script pub keys and we spend the address reused coins together.
		{
			Money target = Money.Satoshis(30000);
			TxOut txOut = new(target, scriptPubKey: BitcoinFactory.CreateScript());

			var suggestions = ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(availableCoins, FeeRate.Zero, txOut, maxInputCount: 6, testDeadlineCts.Token);

			await foreach (var coins in suggestions)
			{
				// We expect all address-reused coins to be selected together.
				Assert.Single(coins.GroupBy(coin => coin.ScriptPubKey));

				long sumOfCoins = coins.Sum(coin => coin.Amount);

				if (coins.All(coin => coin.ScriptPubKey == constantHdPubKey.P2wpkhScript))  // Less-selection strategy
				{
					Assert.Equal(30_000, sumOfCoins);
					Assert.Equal(3, coins.Count);
				}
				else if (coins.All(coin => coin.ScriptPubKey == constantHdPubKey2.P2wpkhScript))   // More-selection strategy
				{
					Assert.Equal(35_000, sumOfCoins);
					Assert.Equal(4, coins.Count);
				}
				else
				{
					Assert.Fail("Mixed scripts in coin selection!");
				}
			}
		}

		// Second test case where we show that a final selection can contain coins with multiple script pub keys.
		{
			Money target = Money.Satoshis(60_000);
			TxOut txOut = new(target, scriptPubKey: BitcoinFactory.CreateScript());

			var suggestions = ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(availableCoins, FeeRate.Zero, txOut, maxInputCount: 10, testDeadlineCts.Token);

			await foreach (var coins in suggestions)
			{
				long sumOfCoins = coins.Sum(coin => coin.Amount);

				if (sumOfCoins == 65_000) // Second case: More-selection strategy.
				{
					Assert.True(coins.All(coin => coin.ScriptPubKey == constantHdPubKey.P2wpkhScript || coin.ScriptPubKey == constantHdPubKey2.P2wpkhScript));
					Assert.Equal(65_000, coins.Sum(coin => coin.Amount));
					Assert.Equal(7, coins.Count);
				}
				else
				{
					Assert.Fail("Unexpected selection!");
				}
			}
		}
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void MatchesLegacySelectionObjective(bool more)
	{
		Random random = new(712_389);
		var pool = GenerateDummySmartCoins(TestRandom.Get(), ServiceFactory.CreateKeyManager(), Enumerable.Range(1, 12).Select(x => (long)x).ToArray());
		for (int iteration = 0; iteration < 150; iteration++)
		{
			var inputs = pool.Take(random.Next(1, 13))
				.Select(coin => (Coin: coin, Value: (long)random.Next(1, 51), Cost: (long)random.Next(0, 15)))
				.OrderByDescending(x => x.Value).ToArray();
			Dictionary<SmartCoin[], long> groups = inputs.ToDictionary(x => new[] { x.Coin }, x => x.Value);
			StrategyParameters parameters = new(random.Next(1, (int)inputs.Sum(x => x.Value) + 20), inputs.Select(x => x.Value).ToArray(), inputs.Select(x => x.Cost).ToArray(), random.Next(1, inputs.Length + 1));
			SelectionStrategy legacy = more ? new LegacyMoreStrategy(parameters) : new LegacyLessStrategy(parameters);
			new BranchAndBound().TryGetMatch(legacy, out _, CancellationToken.None);
			SelectionStrategy strategy = more ? new MoreSelectionStrategy(parameters) : new LessSelectionStrategy(parameters);
			bool found = ChangelessTransactionCoinSelector.TryGetCoins(strategy, groups, out var selected, CancellationToken.None);
			long[]? expected = legacy.GetBestSelectionFound();
			Assert.Equal(expected is not null, found);
			if (expected is not null)
			{
				Assert.Equal(expected.Sum(), inputs.Where(x => selected!.Contains(x.Coin)).Sum(x => x.Value));
				long expectedCosts = more ? ((LegacyMoreStrategy)legacy).TotalCosts : ((LegacyLessStrategy)legacy).TotalCosts;
				Assert.Equal(expectedCosts, inputs.Where(x => selected!.Contains(x.Coin)).Sum(x => x.Value + x.Cost));
				Assert.InRange(selected!.Count, 1, parameters.MaxInputCount);
			}
		}
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void EqualEffectiveValuesSelectCheaperGroup(bool more)
	{
		var coins = GenerateDummySmartCoins(TestRandom.Get(), ServiceFactory.CreateKeyManager(), 120, 101);
		Dictionary<SmartCoin[], long> groups = new() { [new[] { coins[0] }] = 100, [new[] { coins[1] }] = 100 };
		StrategyParameters parameters = new(100, new long[] { 100, 100 }, new long[] { 20, 1 });
		SelectionStrategy strategy = more ? new MoreSelectionStrategy(parameters) : new LessSelectionStrategy(parameters);

		Assert.True(ChangelessTransactionCoinSelector.TryGetCoins(strategy, groups, out var selected, CancellationToken.None));
		Assert.Same(coins[1], Assert.Single(selected!));
	}

	[Theory]
	[InlineData(74, 0)]
	[InlineData(75, 1)]
	[InlineData(101, 2)]
	[InlineData(126, 1)]
	[InlineData(127, 0)]
	public async Task PaymentBoundsAreInclusiveAndTruncatedAsync(long amount, int expectedCount)
	{
		HdPubKey key = BitcoinFactory.CreateHdPubKey(ServiceFactory.CreateKeyManager());
		var coins = new[] { BitcoinFactory.CreateSmartCoin(TestRandom.Get(), key, Money.Satoshis(amount) + FeeRate.Zero.GetFee(key.P2wpkhScript.EstimateInputVsize())) };
		TxOut output = new(Money.Zero, BitcoinFactory.CreateScript());
		output.Value = Money.Satoshis(101) - FeeRate.Zero.GetFee(output.ScriptPubKey.EstimateOutputVsize());
		int count = 0;
		await foreach (var selected in ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(coins, FeeRate.Zero, output, 1, CancellationToken.None))
		{
			Assert.Same(coins[0], Assert.Single(selected));
			count++;
		}
		Assert.Equal(expectedCount, count);
	}

	[Theory]
	[InlineData(0.1)]
	[InlineData(1.2)]
	[InlineData(25)]
	public async Task InputLimitCountsGroupsAndIncludesAllTheirCoinsAsync(decimal satoshisPerVbyte)
	{
		var random = TestRandom.Get();
		KeyManager km = ServiceFactory.CreateKeyManager();
		HdPubKey key = BitcoinFactory.CreateHdPubKey(km);
		SmartCoin[] group =
		{
			BitcoinFactory.CreateSmartCoin(random, key, Money.Satoshis(10_000)),
			BitcoinFactory.CreateSmartCoin(random, key, Money.Satoshis(1))
		};
		FeeRate feeRate = new(satoshisPerVbyte);
		TxOut output = new(Money.Zero, BitcoinFactory.CreateScript());
		output.Value = Money.Satoshis(group.Sum(x => x.EffectiveValue(feeRate).Satoshi) - feeRate.GetFee(output.ScriptPubKey.EstimateOutputVsize()).Satoshi);
		var coins = group.Append(BitcoinFactory.CreateSmartCoin(random, BitcoinFactory.CreateHdPubKey(km), Money.Satoshis(1))).ToArray();
		int count = 0;
		await foreach (var selected in ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(coins, feeRate, output, 1, CancellationToken.None))
		{
			Assert.Equal(group, selected);
			count++;
		}
		Assert.Equal(2, count);
		await foreach (var _ in ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(coins, feeRate, output, 0, CancellationToken.None))
		{
			Assert.Fail("A zero group limit must not produce a selection.");
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task EmptyOrUneconomicalGroupsProduceNoSuggestionsAsync(bool empty)
	{
		var coins = GenerateDummySmartCoins(TestRandom.Get(), ServiceFactory.CreateKeyManager(), empty ? Array.Empty<long>() : new long[] { 1, 2 });
		TxOut output = new(Money.Satoshis(100), BitcoinFactory.CreateScript());
		await foreach (var _ in ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(coins, new FeeRate(Money.Satoshis(2)), output, 2, CancellationToken.None))
		{
			Assert.Fail("No positive effective input value is available.");
		}
	}

	[Fact]
	public async Task PreCancelledRequestThrowsAsync()
	{
		var coins = GenerateDummySmartCoins(TestRandom.Get(), ServiceFactory.CreateKeyManager(), 100);
		TxOut output = new(Money.Satoshis(100), BitcoinFactory.CreateScript());
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
		{
			await foreach (var _ in ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(coins, FeeRate.Zero, output, 1, new CancellationToken(canceled: true)))
			{
			}
		});
	}

	private sealed class LegacyMoreStrategy : MoreSelectionStrategy
	{
		public LegacyMoreStrategy(StrategyParameters parameters) : base(parameters) { }
		public long TotalCosts => BestSelection.TotalCosts;
	}

	private sealed class LegacyLessStrategy : LessSelectionStrategy
	{
		public LegacyLessStrategy(StrategyParameters parameters) : base(parameters) { }
		public long TotalCosts => BestSelection.TotalCosts;
	}

	/// <remarks>These smart coins are from an invalid transaction but we are interested only in smart coins' amounts.</remarks>
	private List<SmartCoin> GenerateDummySmartCoins(GingerRandom rnd, KeyManager km, params long[] values)
	{
		List<SmartCoin> result = new(capacity: values.Length);

		for (uint i = 0; i < values.Length; i++)
		{
			result.Add(BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km), Money.Satoshis(values[i])));
		}

		return result.OrderByDescending(x => x.Amount.Satoshi).ToList();
	}
}
