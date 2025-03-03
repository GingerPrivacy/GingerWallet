using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using WalletWasabi.Crypto;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Models;
using Xunit;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.Tests.TestCommon;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests;

public class RegisterInputSuccessTests
{
	private static void AssertSingleAliceSuccessfullyRegistered(Round round, DateTimeOffset minAliceDeadline, ArenaResponse<Guid> resp)
	{
		var alice = Assert.Single(round.Alices);
		Assert.NotNull(resp);
		Assert.NotNull(resp.IssuedAmountCredentials);
		Assert.NotNull(resp.IssuedVsizeCredentials);
		Assert.True(minAliceDeadline <= alice.Deadline);
	}

	[Fact]
	public async Task SuccessAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		var round = WabiSabiTestFactory.CreateRound(cfg);

		using Key key = new();
		var coin = WabiSabiTestFactory.CreateCoin(key);
		var rpc = WabiSabiTestFactory.CreatePreconfiguredRpcClient(coin);
		using Arena arena = await ArenaTestFactory.From(cfg).With(rpc).CreateAndStartAsync(round);

		var minAliceDeadline = DateTimeOffset.UtcNow + (cfg.ConnectionConfirmationTimeout * 0.9);
		var arenaClient = WabiSabiTestFactory.CreateArenaClient(arena);
		var ownershipProof = WabiSabiTestFactory.CreateOwnershipProof(key, round.Id);

		var (resp, _) = await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None);
		AssertSingleAliceSuccessfullyRegistered(round, minAliceDeadline, resp);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task SuccessCustomCoordinatorIdentifierAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.CoordinatorIdentifier = "test";
		var round = WabiSabiTestFactory.CreateRound(cfg, 1);

		using Key key = new();
		var coin = WabiSabiTestFactory.CreateCoin(key);
		var rpc = WabiSabiTestFactory.CreatePreconfiguredRpcClient(coin);
		using Arena arena = await ArenaTestFactory.From(cfg).With(rpc).CreateAndStartAsync(round);

		var minAliceDeadline = DateTimeOffset.UtcNow + (cfg.ConnectionConfirmationTimeout * 0.9);

		var roundState = RoundState.FromRound(arena.Rounds.First());
		var arenaClient = new ArenaClient(
			roundState.CreateAmountCredentialClient(TestRandom.Wasabi(2)),
			roundState.CreateVsizeCredentialClient(TestRandom.Wasabi(3)),
			"test",
			arena);
		var ownershipProof = OwnershipProof.GenerateCoinJoinInputProof(key, new OwnershipIdentifier(key, key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit)), new CoinJoinInputCommitmentData("test", round.Id), ScriptPubKeyType.Segwit);

		var (resp, _) = await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None);
		AssertSingleAliceSuccessfullyRegistered(round, minAliceDeadline, resp);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task SuccessFromPreviousCoinJoinAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		var round = WabiSabiTestFactory.CreateRound(cfg);

		using Key key = new();
		var coin = WabiSabiTestFactory.CreateCoin(key);
		var rpc = WabiSabiTestFactory.CreatePreconfiguredRpcClient(coin);
		var coinJoinIdStore = new CoinJoinIdStore();
		coinJoinIdStore.TryAdd(coin.Outpoint.Hash);
		using Arena arena = await ArenaTestFactory.From(cfg).With(rpc).With(coinJoinIdStore).CreateAndStartAsync(round);

		var minAliceDeadline = DateTimeOffset.UtcNow + (cfg.ConnectionConfirmationTimeout * 0.9);
		var arenaClient = WabiSabiTestFactory.CreateArenaClient(arena);
		var ownershipProof = WabiSabiTestFactory.CreateOwnershipProof(key, round.Id);

		var (resp, _) = await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None);
		AssertSingleAliceSuccessfullyRegistered(round, minAliceDeadline, resp);

		var myAlice = Assert.Single(round.Alices);
		Assert.True(myAlice.IsCoordinationFeeExempted);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task SuccessWithAliceUpdateIntraRoundAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		var round = WabiSabiTestFactory.CreateRound(cfg);

		using Key key = new();
		var ownershipProof = WabiSabiTestFactory.CreateOwnershipProof(key, round.Id);
		var coin = WabiSabiTestFactory.CreateCoin(key);

		// Make sure an Alice have already been registered with the same input.
		var preAlice = WabiSabiTestFactory.CreateAlice(coin, WabiSabiTestFactory.CreateOwnershipProof(key), round);
		round.Alices.Add(preAlice);

		var rpc = WabiSabiTestFactory.CreatePreconfiguredRpcClient(coin);
		using Arena arena = await ArenaTestFactory.From(cfg).With(rpc).CreateAndStartAsync(round);

		var arenaClient = WabiSabiTestFactory.CreateArenaClient(arena);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None).ConfigureAwait(false));
		Assert.Equal(WabiSabiProtocolErrorCode.AliceAlreadyRegistered, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task TaprootSuccessAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.AllowP2trInputs = true;

		var round = WabiSabiTestFactory.CreateRound(cfg);

		using Key key = new();
		var coin = WabiSabiTestFactory.CreateCoin(key, scriptPubKeyType: ScriptPubKeyType.TaprootBIP86);
		var rpc = WabiSabiTestFactory.CreatePreconfiguredRpcClient(coin);
		using Arena arena = await ArenaTestFactory.From(cfg).With(rpc).CreateAndStartAsync(round);

		var minAliceDeadline = DateTimeOffset.UtcNow + (cfg.ConnectionConfirmationTimeout * 0.9);
		var arenaClient = WabiSabiTestFactory.CreateArenaClient(arena);
		var ownershipProof = WabiSabiTestFactory.CreateOwnershipProof(key, round.Id, ScriptPubKeyType.TaprootBIP86);

		var (resp, _) = await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None);
		AssertSingleAliceSuccessfullyRegistered(round, minAliceDeadline, resp);

		await arena.StopAsync(CancellationToken.None);
	}
}
