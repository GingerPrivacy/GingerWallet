using Moq;
using NBitcoin;
using System.Linq;
using System.Threading.Tasks;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

/// <summary>
/// Tests for <see cref="CoinJoinCoinSelector"/>.
/// </summary>
public class CoinJoinCoinSelectionTests
{
	public static RoundParameters CreateRoundParameters()
	{
		var cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.MinRegistrableAmount = Money.Coins(0.0001m);
		cfg.MaxRegistrableAmount = Money.Coins(430);
		return WabiSabiTestFactory.CreateRoundParameters(cfg);
	}

	/// <summary>
	/// This test is to make sure no coins are selected when there are no coins.
	/// </summary>
	[Fact]
	public async Task SelectNothingFromEmptySetOfCoinsAsync()
	{
		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: 10, semiPrivateThreshold: 0, generator);
		var coins = await coinJoinCoinSelector.SelectCoinsForRoundAsync(
			coins: Enumerable.Empty<SmartCoin>(),
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Empty(coins);
	}

	/// <summary>
	/// This test is to make sure no coins are selected when all coins are private.
	/// </summary>
	[Fact]
	public async Task SelectNothingFromFullyPrivateSetOfCoinsAsync()
	{
		var rnd = TestRandom.Get();
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km, isInternal: true), Money.Coins(1m), anonymitySet: AnonymitySet + 1))
			.ToList();

		// We gotta make sure the distance from external keys is sufficient.
		foreach (var sc in coinsToSelectFrom)
		{
			var sci = BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km, isInternal: true), Money.Coins(1m), anonymitySet: AnonymitySet + 1);
			sci.Transaction.TryAddWalletInput(BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km, isInternal: true), Money.Coins(1m), anonymitySet: AnonymitySet + 1));
			sc.Transaction.TryAddWalletInput(sci);
		}
		foreach (var sc in coinsToSelectFrom)
		{
			BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(sc);
		}

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);
		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);

		var coins = await coinJoinCoinSelector.SelectCoinsForRoundAsync(
			coins: coinsToSelectFrom,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Empty(coins);
	}

	/// <summary>
	/// This test is to make sure no coins are selected when there too small coins.
	/// Although the coin amount is larger than the smallest reasonable effective denomination, if the algorithm is right, then the effective input amount is considered.
	/// </summary>
	[Fact]
	public async Task SelectSomethingFromPrivateButExternalSetOfCoins1Async()
	{
		var rnd = TestRandom.Get();
		// Although all coins have reached the desired anonymity set, they are not sufficiently distanced from external keys, because they are external keys.
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km, isInternal: false), Money.Coins(1m), anonymitySet: AnonymitySet + 1))
			.ToList();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);
		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);
		var coins = await coinJoinCoinSelector.SelectCoinsForRoundAsync(
			coins: coinsToSelectFrom,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.NotEmpty(coins);
	}

	[Fact]
	public async Task SelectSomethingFromPrivateButNotDistancedSetOfCoins2Async()
	{
		var rnd = TestRandom.Get();
		// Although all coins have reached the desired anonymity set, they are not sufficiently distanced from external keys.
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km, isInternal: true), Money.Coins(1m), anonymitySet: AnonymitySet + 1))
			.ToList();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);
		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);
		var coins = await coinJoinCoinSelector.SelectCoinsForRoundAsync(
			coins: coinsToSelectFrom,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.NotEmpty(coins);
	}

	[Fact]
	public async Task SelectSomethingFromPrivateButExternalSetOfCoins3Async()
	{
		var rnd = TestRandom.Get();
		// Although all coins have reached the desired anonymity set, they are not sufficiently distanced from external keys.
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km, isInternal: true), Money.Coins(1m), anonymitySet: AnonymitySet + 1))
			.ToList();

		// We gotta make sure the distance from external keys is sufficient.
		foreach (var sc in coinsToSelectFrom)
		{
			sc.Transaction.TryAddWalletInput(BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km, isInternal: false), Money.Coins(1m), anonymitySet: AnonymitySet + 1));
		}
		foreach (var sc in coinsToSelectFrom)
		{
			BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(sc);
		}

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);
		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);
		var coins = await coinJoinCoinSelector.SelectCoinsForRoundAsync(
			coins: coinsToSelectFrom,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.NotEmpty(coins);
	}

	[Fact]
	public async Task SelectNothingFromTooSmallCoinAsync()
	{
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = new[] { BitcoinFactory.CreateSmartCoin(TestRandom.Get(), BitcoinFactory.CreateHdPubKey(km), Money.Coins(0.00017423m), anonymitySet: 1) };
		var roundParams = CreateRoundParameters();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: 10, semiPrivateThreshold: 0, generator);
		var coins = await coinJoinCoinSelector.SelectCoinsForRoundAsync(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(roundParams, [ScriptType.P2WPKH, ScriptType.Taproot]),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Empty(coins);
	}

	/// <summary>
	/// This test is to make sure no coins are selected when there too small coins.
	/// </summary>
	[Fact]
	public async Task SelectNothingFromTooSmallSetOfCoinsAsync()
	{
		var rnd = TestRandom.Get();
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = new[]
		{
			BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km), Money.Coins(0.00008711m + 0.00006900m), anonymitySet: 1),
			BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km), Money.Coins(0.00008710m + 0.00006900m), anonymitySet: 1)
		};
		var roundParams = CreateRoundParameters();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: 10, semiPrivateThreshold: 0, generator);
		var coins = await coinJoinCoinSelector.SelectCoinsForRoundAsync(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(roundParams, [ScriptType.P2WPKH, ScriptType.Taproot]),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Empty(coins);
	}

	/// <summary>
	/// This test is to make sure the coins are selected when the selection's effective sum is exactly the smallest reasonable effective denom.
	/// </summary>
	[Fact]
	public async Task SelectSomethingFromJustEnoughSetOfCoinsAsync()
	{
		var rnd = TestRandom.Get();
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = new[]
		{
			BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km), Money.Coins(0.00008711m + 0.00006900m), anonymitySet: 1),
			BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km), Money.Coins(0.00008711m + 0.00006900m), anonymitySet: 1)
		};
		var roundParams = CreateRoundParameters();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: 10, semiPrivateThreshold: 0, generator);
		var coins = await coinJoinCoinSelector.SelectCoinsForRoundAsync(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(roundParams, [ScriptType.P2WPKH, ScriptType.Taproot]),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.NotEmpty(coins);
	}

	/// <summary>
	/// This test is to make sure that we select the non-private coin in the set.
	/// </summary>
	[Fact]
	public async Task SelectNonPrivateCoinFromOneNonPrivateCoinInBigSetOfCoinsConsolidationModeAsync()
	{
		var rnd = TestRandom.Get();
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		SmartCoin smallerAnonCoin = BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet + 1))
			.Prepend(smallerAnonCoin)
			.ToList();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: true, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);
		var coins = await coinJoinCoinSelector.SelectCoinsForRoundAsync(
			coins: coinsToSelectFrom,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Contains(smallerAnonCoin, coins);
		Assert.Equal(10, coins.Count);
	}

	/// <summary>
	/// This test is to make sure that we select the only non-private coin when it is the only coin in the wallet.
	/// </summary>
	[Fact]
	public async Task SelectNonPrivateCoinFromOneCoinSetOfCoinsAsync()
	{
		var rnd = TestRandom.Get();
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.ToList();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 10);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);
		var coins = await coinJoinCoinSelector.SelectCoinsForRoundAsync(
			coins: coinsToSelectFrom,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Single(coins);
	}

	/// <summary>
	/// This test is to make sure that we select more non-private coins when they are coming from different txs.
	/// </summary>
	/// <remarks>Note randomization can make this test fail even though that's unlikely.</remarks>
	[Fact]
	public async Task SelectMoreNonPrivateCoinFromTwoCoinsSetOfCoinsAsync()
	{
		var rnd = TestRandom.Get();
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.Prepend(BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.ToList();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 10, sameTxAllowance: 0);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);
		var coins = await coinJoinCoinSelector.SelectCoinsForRoundAsync(
			coins: coinsToSelectFrom,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Equal(2, coins.Count);
	}

	/// <summary>
	/// This test is to make sure that we select more than one non-private coin.
	/// </summary>
	[Fact]
	public async Task SelectTwoNonPrivateCoinsFromTwoCoinsSetOfCoinsConsolidationModeAsync()
	{
		var rnd = TestRandom.Get();
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.Prepend(BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.ToList();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 10);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: true, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);
		var coins = await coinJoinCoinSelector.SelectCoinsForRoundAsync(
			coins: coinsToSelectFrom,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Equal(2, coins.Count);
	}

	/// <summary>
	/// This test is to make sure no coins are selected when all coins are private.
	/// </summary>
	[Fact]
	public async Task SelectNothingFromFullyPrivateAndBelowMinAllowedSetOfCoinsAsync()
	{
		var rnd = TestRandom.Get();
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km, isInternal: true), Money.Coins(1m), anonymitySet: AnonymitySet + 1))
			.ToList();

		var utxoSelectionParameter = CreateUtxoSelectionParameters();
		coinsToSelectFrom.Add(BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km, isInternal: true), utxoSelectionParameter.AllowedInputAmounts.Min - Money.Satoshis(1), anonymitySet: AnonymitySet - 1));

		// We gotta make sure the distance from external keys is sufficient.
		foreach (var sc in coinsToSelectFrom)
		{
			var sci = BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km, isInternal: true), Money.Coins(1m), anonymitySet: AnonymitySet + 1);
			sci.Transaction.TryAddWalletInput(BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km, isInternal: true), Money.Coins(1m), anonymitySet: AnonymitySet + 1));
			sc.Transaction.TryAddWalletInput(sci);
		}
		foreach (var sc in coinsToSelectFrom)
		{
			BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(sc);
		}

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);
		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);

		var coins = await coinJoinCoinSelector.SelectCoinsForRoundAsync(
			coins: coinsToSelectFrom,
			utxoSelectionParameter,
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Empty(coins);
	}

	private static CoinJoinCoinSelectorRandomnessGenerator CreateSelectorGenerator(int inputTarget, int? sameTxAllowance = null)
	{
		WasabiRandom rng = InsecureRandom.Instance;
		Mock<CoinJoinCoinSelectorRandomnessGenerator> mockGenerator = new(MockBehavior.Loose, CoinJoinCoinSelector.MaxInputsRegistrableByWallet, rng) { CallBase = true };
		mockGenerator.Setup(c => c.GetInputTarget())
			.Returns(inputTarget);

		if (sameTxAllowance is not null)
		{
			mockGenerator.Setup(c => c.GetRandomBiasedSameTxAllowance(It.IsAny<int>()))
				.Returns(sameTxAllowance.Value);
		}

		return mockGenerator.Object;
	}

	private static RoundParameters CreateMultipartyTransactionParameters()
	{
		var roundParams = CreateRoundParameters();
		return roundParams;
	}

	private static UtxoSelectionParameters CreateUtxoSelectionParameters() =>
		UtxoSelectionParameters.FromRoundParameters(
			CreateMultipartyTransactionParameters(),
			[ScriptType.P2WPKH, ScriptType.Taproot]);
}
