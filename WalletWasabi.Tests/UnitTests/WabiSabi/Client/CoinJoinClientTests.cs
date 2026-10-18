using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.Extensions;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.Tests.UnitTests.WabiSabi.Integration;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class CoinJoinClientTests
{
	[Theory]
	[InlineData(6, 2, false, 1.00, true)]
	[InlineData(5, 2, false, 1.00, false)]
	[InlineData(6, 6, false, 1.00, false)]
	[InlineData(6, 6, true, 1.00, true)]
	[InlineData(6, 2, false, 0.89, false)]
	[InlineData(6, 2, false, 0.90, false)]
	[InlineData(6, 2, false, 0.91, true)]
	public void SigningChecksActualInputsAndMiningFee(int inputCount, int ownInputCount, bool allowSolo, decimal feeRatio, bool expected)
	{
		var parameters = WabiSabiTestFactory.CreateRoundParameters(WabiSabiTestFactory.CreateDefaultWabiSabiConfig());
		var inputs = Enumerable.Range(0, inputCount).Select(_ => WabiSabiTestFactory.CreateCoin()).ToArray();
		var output = new TxOut(Money.Zero, BitcoinFactory.CreateScript());
		var vsize = inputs.Sum(x => x.ScriptPubKey.EstimateInputVsize()) + output.ScriptPubKey.EstimateOutputVsize();
		output.Value = inputs.Sum(x => x.Amount) - Money.Satoshis((long)(parameters.MiningFeeRate.GetFee(vsize).Satoshi * feeRatio));
		var state = new SigningState(parameters, new IEvent[] { new RoundCreated(parameters), new OutputAdded(output) }
			.Concat(inputs.Select(x => new InputAdded(x, null!))));
		var configuration = new CoinJoinConfiguration("test", 0.3m, 150m, 6, allowSolo);

		Assert.Equal(expected, CoinJoinClient.IsSigningStateSafe(state, ownInputCount, configuration));
	}

	[Fact]
	public void SanityCheckTest()
	{
		var output1 = new TxOut(Money.Coins(1), BitcoinFactory.CreateScript());
		var output2 = new TxOut(Money.Coins(2), BitcoinFactory.CreateScript());
		var output3 = new TxOut(Money.Coins(3), BitcoinFactory.CreateScript());
		var output4 = new TxOut(Money.Coins(4), BitcoinFactory.CreateScript());

		// Exact match (one expected)
		Assert.True(CoinJoinClient.SanityCheck(
			new[] { output1 },
			new[] { output1, output2, output3, output4 }));

		// Exact match (two expected)
		Assert.True(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, output2, output3, output4 }));

		// Missing output
		Assert.False(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, output2, output4 }));

		static TxOut AddSats(long sats, TxOut output) => new(output.Value + sats, output.ScriptPubKey);
		static TxOut AddOneSat(TxOut output) => AddSats(1, output);
		static TxOut SubOneSat(TxOut output) => AddSats(-1, output);

		// More money in one output
		Assert.True(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, AddOneSat(output2), output3, output4 }));

		// More money in all output
		Assert.True(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, AddOneSat(output2), AddOneSat(output3), output4 }));

		// Same scriptpubkeys, same amount of money but outputs were manipulated
		Assert.False(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, AddOneSat(output2), SubOneSat(output3), output4 }));
	}

	[Theory]
	[InlineData(false, false, true)]
	[InlineData(true, false, false)]
	[InlineData(false, true, false)]
	public void BlameRoundRequiresPreviousInputsAndSuggestedAmount(bool foreignInput, bool changedAmount, bool expected)
	{
		var parameters = WabiSabiTestFactory.CreateRoundParameters(WabiSabiTestFactory.CreateDefaultWabiSabiConfig());
		var original = WabiSabiTestFactory.CreateCoin();
		var previous = new DisruptedCoinJoinResult([], ImmutableHashSet.Create(original.Outpoint), parameters.MaxSuggestedAmount);
		if (changedAmount)
		{
			parameters = parameters with { MaxSuggestedAmount = parameters.MaxSuggestedAmount - Money.Satoshis(1) };
		}
		var input = foreignInput ? WabiSabiTestFactory.CreateCoin() : original;
		var state = new SigningState(parameters, [new RoundCreated(parameters), new InputAdded(input, null!)]);

		Assert.Equal(expected, CoinJoinClient.IsBlameRoundValid(state, previous));
	}

	[Theory]
	[InlineData(3, true, false, 3)]
	[InlineData(3, false, false, 2)]
	[InlineData(2, true, false, 1)]
	[InlineData(2, false, false, 1)]
	[InlineData(1, true, false, 0)]
	[InlineData(1, false, false, 0)]
	[InlineData(0, true, false, 0)]
	[InlineData(0, false, false, 0)]
	[InlineData(3, true, true, 0)]
	[InlineData(3, false, true, 0)]
	[InlineData(2, true, true, 0)]
	[InlineData(2, false, true, 0)]
	public async Task SigningOnlySignsPresentInputsAndWithholdsOneForUnsafeRoundsAsync(int presentInputCount, bool includeOwnOutput, bool duplicateOwnInput, int expectedSignatureCount)
	{
		var km = ServiceFactory.CreateKeyManager("");
		var keyChain = new KeyChain(km, new Kitchen(""));
		var rnd = TestRandom.Get();
		var ownCoins = Enumerable.Range(0, 3).Select(_ => BitcoinFactory.CreateSmartCoin(rnd, BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m))).ToArray();
		var presentOwnCoins = ownCoins.Take(presentInputCount).ToArray();
		var ownInputs = presentOwnCoins.Select(x => x.Coin);
		if (duplicateOwnInput)
		{
			ownInputs = ownInputs.Append(presentOwnCoins[0].Coin);
		}
		var inputs = ownInputs.Concat(Enumerable.Range(0, 4).Select(_ => WabiSabiTestFactory.CreateCoin())).ToArray();
		var parameters = WabiSabiTestFactory.CreateRoundParameters(WabiSabiTestFactory.CreateDefaultWabiSabiConfig())
			with { TransactionSigningTimeout = TimeSpan.FromMilliseconds(250) };

		var expectedOutput = new TxOut(Money.Coins(2.99m), BitcoinFactory.CreateScript());
		TxOut[] ownOutputs = includeOwnOutput ? [expectedOutput] : [];
		var otherOutput = new TxOut(Money.Zero, BitcoinFactory.CreateScript());
		var outputs = ownOutputs.Append(otherOutput).ToArray();
		var vsize = inputs.Sum(x => x.ScriptPubKey.EstimateInputVsize()) + outputs.Sum(x => x.ScriptPubKey.EstimateOutputVsize());
		otherOutput.Value = inputs.Sum(x => x.Amount) - ownOutputs.Sum(x => x.Value) - parameters.MiningFeeRate.GetFee(vsize);
		var state = new SigningState(parameters, new IEvent[] { new RoundCreated(parameters) }
			.Concat(inputs.Select(x => new InputAdded(x, null!)))
			.Concat(outputs.Select(x => new OutputAdded(x))));
		var roundState = RoundState.FromRound(WabiSabiTestFactory.CreateRound(parameters)) with { CoinjoinState = state, Phase = Phase.TransactionSigning };

		var signatures = new ConcurrentQueue<TransactionSignaturesRequest>();
		var api = new Mock<IWabiSabiApiRequestHandler>(MockBehavior.Strict);
		api.Setup(x => x.GetStatusAsync(It.IsAny<RoundStateRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new RoundStateResponse([roundState], []));
		api.Setup(x => x.SignTransactionAsync(It.IsAny<TransactionSignaturesRequest>(), It.IsAny<CancellationToken>()))
			.Callback<TransactionSignaturesRequest, CancellationToken>((request, _) => signatures.Enqueue(request))
			.Returns(Task.CompletedTask);

		var configuration = new CoinJoinConfiguration("test", 0.3m, 150m, 6, AllowSoloCoinjoining: false);
		var arenaClient = new ArenaClient(null!, null!, configuration.CoordinatorIdentifier, api.Object);
		var aliceConstructor = typeof(AliceClient).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
		var aliceClients = ownCoins.Select(coin => (AliceClient)aliceConstructor.Invoke(
			[Guid.NewGuid(), roundState, arenaClient, coin, Array.Empty<Credential>(), Array.Empty<Credential>(), true])).ToImmutableArray();

		using var roundStateUpdater = new RoundStateUpdater(TimeSpan.FromMilliseconds(10), [], api.Object, verifyRoundState: false);
		var coinJoinClient = new CoinJoinClient(
			new MockWasabiHttpClientFactory(), keyChain, new OutputProvider(null!), roundStateUpdater,
			new CoinJoinCoinSelector(true, int.MaxValue, 0), configuration, new LiquidityClueProvider());
		var proceedWithSigning = typeof(CoinJoinClient).GetMethod("ProceedWithSigningStateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var signingTask = (Task<(Transaction UnsignedCoinJoin, ImmutableArray<AliceClient> AliceClientsThatSigned)>)proceedWithSigning.Invoke(
			coinJoinClient, [roundState.Id, aliceClients, new[] { expectedOutput }, cts.Token])!;
		await roundStateUpdater.StartAsync(cts.Token);
		try
		{
			var exception = await Record.ExceptionAsync(() => signingTask.WaitAsync(cts.Token));

			if (duplicateOwnInput)
			{
				Assert.Empty(signatures);
				var coinJoinClientException = Assert.IsType<CoinJoinClientException>(exception);
				Assert.Equal(CoinjoinError.TamperedRoundState, coinJoinClientException.CoinjoinError);
			}
			else
			{
				Assert.Null(exception);
				var (transaction, aliceClientsThatSigned) = await signingTask;
				Assert.Equal(expectedSignatureCount, aliceClientsThatSigned.Length);
				Assert.Equal(expectedSignatureCount, signatures.Count);
				var signedOutpoints = aliceClientsThatSigned.Select(x => x.SmartCoin.Outpoint).ToArray();
				Assert.Distinct(signedOutpoints);
				Assert.All(signedOutpoints, outpoint => Assert.Contains(outpoint, presentOwnCoins.Select(x => x.Outpoint)));
				Assert.Equal(
					signedOutpoints.Select(x => x.ToString()).Order(),
					signatures.Select(x => transaction.Inputs[(int)x.InputIndex].PrevOut.ToString()).Order());
				foreach (var signature in signatures)
				{
					state = state.AddWitness((int)signature.InputIndex, signature.Witness);
				}
			}
		}
		finally
		{
			await roundStateUpdater.StopAsync(CancellationToken.None);
		}
	}

	[Fact]
	public void GetTxOutsTest()
	{
		FeeRate feeRate = new(10m);

		var outputs = new[]
		{
			Output.FromDenomination(Money.Coins(1m), ScriptType.P2WPKH, feeRate),
			Output.FromDenomination(Money.Coins(2m), ScriptType.P2WPKH, feeRate),
			Output.FromDenomination(Money.Coins(3m), ScriptType.Taproot, feeRate),
			Output.FromDenomination(Money.Coins(4m), ScriptType.Taproot, feeRate),
		};

		var password = "satoshi";
		var km = ServiceFactory.CreateKeyManager(password, true);
		var destinationProvider = new InternalDestinationProvider(km);

		var txOuts = OutputProvider.GetTxOuts(outputs, destinationProvider);

		// All the outputs were generated.
		Assert.Equal(txOuts.Count(), outputs.Length);

		// No address reuse.
		Assert.Distinct(txOuts.Select(x => x.ScriptPubKey));

		// Verify if all the outputs are generated with correct ScriptType and Value.
		List<TxOut> toCheck = txOuts.ToList();
		foreach (var output in outputs)
		{
			var foundTxOut = toCheck.First(txout => txout.ScriptPubKey.IsScriptType(output.ScriptType) && txout.Value == output.Amount);
			toCheck.Remove(foundTxOut);
		}

		Assert.Empty(toCheck);
	}
}
