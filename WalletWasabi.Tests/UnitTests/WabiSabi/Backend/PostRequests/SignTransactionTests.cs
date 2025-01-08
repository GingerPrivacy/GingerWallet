using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests;

public class SignTransactionTests
{
	[Fact]
	public async Task SuccessAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		var round = WabiSabiTestFactory.CreateRound(cfg);
		using Key key = new();
		Alice alice = WabiSabiTestFactory.CreateAlice(key: key, round: round);
		round.Alices.Add(alice);
		round.CoinjoinState = round.AddInput(alice.Coin, alice.OwnershipProof, WabiSabiTestFactory.CreateCommitmentData(round.Id)).Finalize();
		round.SetPhase(Phase.TransactionSigning);
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		var aliceSignedCoinJoin = round.Assert<SigningState>().CreateUnsignedTransaction();
		aliceSignedCoinJoin.Sign(key.GetBitcoinSecret(Network.Main), alice.Coin);

		var req = new TransactionSignaturesRequest(round.Id, 0, aliceSignedCoinJoin.Inputs[0].WitScript);
		await arena.SignTransactionAsync(req, CancellationToken.None);
		Assert.True(round.Assert<SigningState>().IsFullySigned);
		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task TaprootSuccessAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.AllowP2trInputs = true;
		cfg.AllowP2trOutputs = true;

		var round = WabiSabiTestFactory.CreateRound(cfg);
		using Key key = new();
		Alice alice = WabiSabiTestFactory.CreateAlice(key: key, round: round, scriptPubKeyType: ScriptPubKeyType.TaprootBIP86);
		round.Alices.Add(alice);
		round.CoinjoinState = round.AddInput(alice.Coin, alice.OwnershipProof, WabiSabiTestFactory.CreateCommitmentData(round.Id)).Finalize();
		round.SetPhase(Phase.TransactionSigning);
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		var aliceSignedCoinJoin = round.Assert<SigningState>().CreateUnsignedTransaction();
		aliceSignedCoinJoin.Sign(key.GetBitcoinSecret(Network.Main), alice.Coin);

		var req = new TransactionSignaturesRequest(round.Id, 0, aliceSignedCoinJoin.Inputs[0].WitScript);
		await arena.SignTransactionAsync(req, CancellationToken.None);
		Assert.True(round.Assert<SigningState>().IsFullySigned);
		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task RoundNotFoundAsync()
	{
		using Arena arena = await ArenaTestFactory.Default.CreateAndStartAsync();
		var req = new TransactionSignaturesRequest(uint256.Zero, 0, WitScript.Empty);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.SignTransactionAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);
		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task WrongPhaseAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync();
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		var round = arena.Rounds.First();

		var req = new TransactionSignaturesRequest(round.Id, 0, WitScript.Empty);
		foreach (Phase phase in Enum.GetValues(typeof(Phase)))
		{
			if (phase != Phase.TransactionSigning)
			{
				round.SetPhase(phase);

				var ex = await Assert.ThrowsAsync<WrongPhaseException>(async () =>
					await arena.SignTransactionAsync(req, CancellationToken.None));
				Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
			}
		}

		await arena.StopAsync(CancellationToken.None);
	}
}
