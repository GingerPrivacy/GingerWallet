using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using NBitcoin.RPC;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WabiSabi.Crypto;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.Backend.Controllers;
using WalletWasabi.Cache;
using WalletWasabi.Crypto;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Wallets;
using Xunit;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.BitcoinCore.Mempool;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.Tests.TestCommon;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class ArenaClientTests
{
	public MempoolMirror DummyMempoolMirror { get; } = new(TimeSpan.Zero, null!, null!);

	[Fact]
	public async Task FullP2wpkhCoinjoinTestAsync()
	{
		await TestFullCoinjoinAsync(ScriptPubKeyType.Segwit, Constants.P2wpkhInputVirtualSize);
	}

	[Fact]
	public async Task FullP2trCoinjoinTestAsync()
	{
		await TestFullCoinjoinAsync(ScriptPubKeyType.TaprootBIP86, Constants.P2trInputVirtualSize);
	}

	[Fact]
	public async Task RemoveInputAsyncTestAsync()
	{
		var config = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		var round = WabiSabiTestFactory.CreateRound(config);
		round.SetPhase(Phase.ConnectionConfirmation);
		var fundingTx = BitcoinFactory.CreateSmartTransaction(ownOutputCount: 1);
		var coin = fundingTx.WalletOutputs.First().Coin;
		var alice = new Alice(coin, new OwnershipProof(), round, Guid.NewGuid(), false);
		round.Alices.Add(alice);

		using Arena arena = await ArenaTestFactory.From(config).CreateAndStartAsync(round);

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var idempotencyRequestCache = new IdempotencyRequestCache(memoryCache);
		using CoinJoinFeeRateStatStore coinJoinFeeRateStatStore = new(config, arena.Rpc);
		using CoinJoinMempoolManager coinJoinMempoolManager = new(new CoinJoinIdStore(), DummyMempoolMirror);
		var wabiSabiApi = new WabiSabiController(idempotencyRequestCache, arena, coinJoinFeeRateStatStore, coinJoinMempoolManager);

		var apiClient = new ArenaClient(null!, null!, config.CoordinatorIdentifier, wabiSabiApi);

		round.SetPhase(Phase.InputRegistration);

		await apiClient.RemoveInputAsync(round.Id, alice.Id, CancellationToken.None);
		Assert.Empty(round.Alices);
	}

	[Fact]
	public async Task SignTransactionAsync()
	{
		WabiSabiConfig config = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		Round round = WabiSabiTestFactory.CreateRound(config);
		var password = "satoshi";

		var km = ServiceFactory.CreateKeyManager(password);
		var keyChain = new KeyChain(km, new Kitchen(password));
		var destinationProvider = new InternalDestinationProvider(km);

		var coins = destinationProvider.GetNextDestinations(2, false)
			.Select(destination => (
				Coin: new Coin(BitcoinFactory.CreateOutPoint(), new TxOut(Money.Coins(1.0m), destination)),
				OwnershipProof: keyChain.GetOwnershipProof(destination, WabiSabiTestFactory.CreateCommitmentData(round.Id))))
			.ToArray();

		Alice alice1 = WabiSabiTestFactory.CreateAlice(coins[0].Coin, coins[0].OwnershipProof, round: round);
		round.Alices.Add(alice1);

		Alice alice2 = WabiSabiTestFactory.CreateAlice(coins[1].Coin, coins[1].OwnershipProof, round: round);
		round.Alices.Add(alice2);

		using Arena arena = await ArenaTestFactory.From(config).CreateAndStartAsync(round);

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var idempotencyRequestCache = new IdempotencyRequestCache(memoryCache);

		using CoinJoinFeeRateStatStore coinJoinFeeRateStatStore = new(config, arena.Rpc);
		using CoinJoinMempoolManager coinJoinMempoolManager = new(new CoinJoinIdStore(), DummyMempoolMirror);
		var wabiSabiApi = new WabiSabiController(idempotencyRequestCache, arena, coinJoinFeeRateStatStore, coinJoinMempoolManager);

		var amountClient = new WabiSabiClient(round.AmountCredentialIssuerParameters, TestRandom.Wasabi(1), ProtocolConstants.MaxAmountCredentialValue);
		var vsizeClient = new WabiSabiClient(round.VsizeCredentialIssuerParameters, TestRandom.Wasabi(2), 2000L);
		var apiClient = new ArenaClient(amountClient, vsizeClient, config.CoordinatorIdentifier, wabiSabiApi);

		round.SetPhase(Phase.TransactionSigning);

		var emptyState = round.Assert<ConstructionState>();
		var commitmentData = WabiSabiTestFactory.CreateCommitmentData(round.Id);

		// We can't use ``emptyState.Finalize()` because this is not a valid transaction so we fake it
		var finalizedEmptyState = new SigningState(round.Parameters, emptyState.Events);

		// No inputs in the coinjoin.
		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
				await apiClient.SignTransactionAsync(round.Id, alice1.Coin, keyChain, finalizedEmptyState.CreateUnsignedTransactionWithPrecomputedData(), CancellationToken.None));

		var oneInput = emptyState.AddInput(alice1.Coin, alice1.OwnershipProof, commitmentData).Finalize();
		round.CoinjoinState = oneInput;

		// Trying to sign coins those are not in the coinjoin.
		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
				await apiClient.SignTransactionAsync(round.Id, alice2.Coin, keyChain, oneInput.CreateUnsignedTransactionWithPrecomputedData(), CancellationToken.None));

		var twoInputs = emptyState
			.AddInput(alice1.Coin, alice1.OwnershipProof, commitmentData)
			.AddInput(alice2.Coin, alice2.OwnershipProof, commitmentData)
			.Finalize();
		round.CoinjoinState = twoInputs;

		Assert.False(round.Assert<SigningState>().IsFullySigned);
		var unsigned = round.Assert<SigningState>().CreateUnsignedTransactionWithPrecomputedData();

		await apiClient.SignTransactionAsync(round.Id, alice1.Coin, keyChain, unsigned, CancellationToken.None);
		Assert.True(round.Assert<SigningState>().IsInputSigned(alice1.Coin.Outpoint));
		Assert.False(round.Assert<SigningState>().IsInputSigned(alice2.Coin.Outpoint));

		Assert.False(round.Assert<SigningState>().IsFullySigned);

		await apiClient.SignTransactionAsync(round.Id, alice2.Coin, keyChain, unsigned, CancellationToken.None);
		Assert.True(round.Assert<SigningState>().IsInputSigned(alice2.Coin.Outpoint));

		Assert.True(round.Assert<SigningState>().IsFullySigned);
	}

	private async Task TestFullCoinjoinAsync(ScriptPubKeyType scriptPubKeyType, int inputVirtualSize)
	{
		var config = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		config.MaxInputCountByRound = 1;
		config.AllowP2trInputs = true;
		config.AllowP2trOutputs = true;

		var round = WabiSabiTestFactory.CreateRound(WabiSabiTestFactory.CreateRoundParameters(config));
		using var key = new Key();
		var outpoint = BitcoinFactory.CreateOutPoint();
		var mockRpc = WabiSabiTestFactory.CreatePreconfiguredRpcClient();
		mockRpc.OnGetTxOutAsync = (_, _, _) =>
			new GetTxOutResponse
			{
				IsCoinBase = false,
				Confirmations = 200,
				TxOut = new TxOut(Money.Coins(1m), key.PubKey.GetAddress(scriptPubKeyType, Network.Main)),
			};
		mockRpc.OnEstimateSmartFeeAsync = (_, _) =>
			Task.FromResult(new EstimateSmartFeeResponse
			{
				Blocks = 1000,
				FeeRate = new FeeRate(10m)
			});
		mockRpc.OnGetMempoolInfoAsync = () =>
			Task.FromResult(new MemPoolInfo
			{
				MinRelayTxFee = 1
			});
		mockRpc.OnGetRawTransactionAsync = (_, _) =>
			Task.FromResult(BitcoinFactory.CreateTransaction());

		using Arena arena = await ArenaTestFactory.From(config).With(mockRpc).CreateAndStartAsync(round);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var idempotencyRequestCache = new IdempotencyRequestCache(memoryCache);

		using CoinJoinFeeRateStatStore coinJoinFeeRateStatStore = new(config, arena.Rpc);
		using CoinJoinMempoolManager coinJoinMempoolManager = new(new CoinJoinIdStore(), DummyMempoolMirror);
		var wabiSabiApi = new WabiSabiController(idempotencyRequestCache, arena, coinJoinFeeRateStatStore, coinJoinMempoolManager);

		var roundState = RoundState.FromRound(round);
		var aliceArenaClient = new ArenaClient(
			roundState.CreateAmountCredentialClient(TestRandom.Wasabi(1)),
			roundState.CreateVsizeCredentialClient(TestRandom.Wasabi(2)),
			config.CoordinatorIdentifier,
			wabiSabiApi);
		var ownershipProof = WabiSabiTestFactory.CreateOwnershipProof(key, round.Id, scriptPubKeyType);

		var (inputRegistrationResponse, _) = await aliceArenaClient.RegisterInputAsync(round.Id, outpoint, ownershipProof, CancellationToken.None);
		var aliceId = inputRegistrationResponse.Value;

		var amountsToRequest = new[]
		{
			Money.Coins(.75m) - round.Parameters.MiningFeeRate.GetFee(inputVirtualSize) - round.Parameters.CoordinationFeeRate.GetFee(Money.Coins(1m)),
			Money.Coins(.25m),
		}.Select(x => x.Satoshi).ToArray();

		using var destinationKey1 = new Key();
		using var destinationKey2 = new Key();
		var scriptSize = (long)destinationKey1.PubKey.GetScriptPubKey(scriptPubKeyType).EstimateOutputVsize();

		var vsizesToRequest = new[] { round.Parameters.MaxVsizeAllocationPerAlice - (inputVirtualSize + (2 * scriptSize)), 2 * scriptSize };

		// Phase: Input Registration
		Assert.Equal(Phase.InputRegistration, round.Phase);

		var connectionConfirmationResponse1 = await aliceArenaClient.ConfirmConnectionAsync(
			round.Id,
			aliceId,
			amountsToRequest,
			vsizesToRequest,
			inputRegistrationResponse.IssuedAmountCredentials,
			inputRegistrationResponse.IssuedVsizeCredentials,
			CancellationToken.None);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));
		Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

		// Phase: Connection Confirmation
		var connectionConfirmationResponse2 = await aliceArenaClient.ConfirmConnectionAsync(
			round.Id,
			aliceId,
			amountsToRequest,
			vsizesToRequest,
			connectionConfirmationResponse1.IssuedAmountCredentials,
			connectionConfirmationResponse1.IssuedVsizeCredentials,
			CancellationToken.None);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1));

		// Phase: Output Registration
		Assert.Equal(Phase.OutputRegistration, round.Phase);

		var bobArenaClient = new ArenaClient(
			roundState.CreateAmountCredentialClient(TestRandom.Wasabi(3)),
			roundState.CreateVsizeCredentialClient(TestRandom.Wasabi(4)),
			config.CoordinatorIdentifier,
			wabiSabiApi);

		var reissuanceResponse = await bobArenaClient.ReissueCredentialAsync(
			round.Id,
			amountsToRequest,
			Enumerable.Repeat(scriptSize, 2),
			connectionConfirmationResponse2.IssuedAmountCredentials.Take(ProtocolConstants.CredentialNumber),
			connectionConfirmationResponse2.IssuedVsizeCredentials.Skip(1).Take(ProtocolConstants.CredentialNumber), // first amount is the leftover value
			CancellationToken.None);

		Credential amountCred1 = reissuanceResponse.IssuedAmountCredentials.ElementAt(0);
		Credential amountCred2 = reissuanceResponse.IssuedAmountCredentials.ElementAt(1);
		Credential zeroAmountCred1 = reissuanceResponse.IssuedAmountCredentials.ElementAt(2);
		Credential zeroAmountCred2 = reissuanceResponse.IssuedAmountCredentials.ElementAt(3);

		Credential vsizeCred1 = reissuanceResponse.IssuedVsizeCredentials.ElementAt(0);
		Credential vsizeCred2 = reissuanceResponse.IssuedVsizeCredentials.ElementAt(1);
		Credential zeroVsizeCred1 = reissuanceResponse.IssuedVsizeCredentials.ElementAt(2);
		Credential zeroVsizeCred2 = reissuanceResponse.IssuedVsizeCredentials.ElementAt(3);

		await bobArenaClient.RegisterOutputAsync(
			round.Id,
			destinationKey1.PubKey.GetScriptPubKey(scriptPubKeyType),
			new[] { amountCred1, zeroAmountCred1 },
			new[] { vsizeCred1, zeroVsizeCred1 },
			CancellationToken.None);

		await bobArenaClient.RegisterOutputAsync(
			round.Id,
			destinationKey2.PubKey.GetScriptPubKey(scriptPubKeyType),
			new[] { amountCred2, zeroAmountCred2 },
			new[] { vsizeCred2, zeroVsizeCred2 },
			CancellationToken.None);

		await aliceArenaClient.ReadyToSignAsync(round.Id, aliceId, CancellationToken.None);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));
		Assert.Equal(Phase.TransactionSigning, round.Phase);

		var tx = round.Assert<SigningState>().CreateTransaction();
		Assert.Single(tx.Inputs);
		Assert.Equal(2 + 1, tx.Outputs.Count); // +1 because it pays coordination fees
	}
}
