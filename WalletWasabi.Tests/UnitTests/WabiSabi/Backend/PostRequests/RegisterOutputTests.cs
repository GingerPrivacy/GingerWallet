using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests;

public class RegisterOutputTests
{
	[Fact]
	public async Task SuccessAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		var round = WabiSabiTestFactory.CreateRound(cfg);
		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiTestFactory.CreateOutputRegistrationRequest(round);
		await arena.RegisterOutputAsync(req, CancellationToken.None);
		Assert.NotEmpty(round.Bobs);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task LegacyOutputsSuccessAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.AllowP2pkhOutputs = true;
		cfg.AllowP2shOutputs = true;

		var round = WabiSabiTestFactory.CreateRound(cfg);
		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		// p2pkh
		using Key privKey0 = new();
		var pkhScript = privKey0.PubKey.GetScriptPubKey(ScriptPubKeyType.Legacy);
		var req0 = WabiSabiTestFactory.CreateOutputRegistrationRequest(round, pkhScript, pkhScript.EstimateOutputVsize());
		await arena.RegisterOutputAsync(req0, CancellationToken.None);
		Assert.Single(round.Bobs);

		// p2sh
		using Key privKey1 = new();
		var shScript = privKey1.PubKey.ScriptPubKey.Hash.ScriptPubKey;
		var req1 = WabiSabiTestFactory.CreateOutputRegistrationRequest(round, shScript, shScript.EstimateOutputVsize());
		await arena.RegisterOutputAsync(req1, CancellationToken.None);
		Assert.Equal(2, round.Bobs.Count);
		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task TaprootSuccessAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.AllowP2trOutputs = true;

		var round = WabiSabiTestFactory.CreateRound(cfg);
		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		using Key privKey = new();
		var req = WabiSabiTestFactory.CreateOutputRegistrationRequest(round, privKey.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86), Constants.P2trOutputVirtualSize);
		await arena.RegisterOutputAsync(req, CancellationToken.None);
		Assert.NotEmpty(round.Bobs);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task TaprootNotAllowedAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.AllowP2trOutputs = false;

		var round = WabiSabiTestFactory.CreateRound(cfg);
		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		using Key privKey = new();
		var req = WabiSabiTestFactory.CreateOutputRegistrationRequest(round, privKey.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86), Constants.P2trOutputVirtualSize);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.ScriptNotAllowed, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task RoundNotFoundAsync()
	{
		var cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		var nonExistingRound = WabiSabiTestFactory.CreateRound(cfg);
		using Arena arena = await ArenaTestFactory.Default.CreateAndStartAsync();
		var req = WabiSabiTestFactory.CreateOutputRegistrationRequest(nonExistingRound);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ScriptNotAllowedAsync()
	{
		using Key key = new();
		var outputScript = key.PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main).ScriptPubKey;

		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		RoundParameters parameters = WabiSabiTestFactory.CreateRoundParameters(cfg)
			with
		{ MaxVsizeAllocationPerAlice = Constants.P2wpkhInputVirtualSize + outputScript.EstimateOutputVsize() };
		var round = WabiSabiTestFactory.CreateRound(parameters);

		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(Money.Coins(1), round));

		var req = WabiSabiTestFactory.CreateOutputRegistrationRequest(round, outputScript);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.ScriptNotAllowed, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task NonStandardOutputAsync()
	{
		var sha256Bounty = Script.FromHex("aa20000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f87");
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		RoundParameters parameters = WabiSabiTestFactory.CreateRoundParameters(cfg)
			with
		{ MaxVsizeAllocationPerAlice = Constants.P2wpkhInputVirtualSize + sha256Bounty.EstimateOutputVsize() };
		var round = WabiSabiTestFactory.CreateRound(parameters);
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(Money.Coins(1), round));

		var req = WabiSabiTestFactory.CreateOutputRegistrationRequest(round, sha256Bounty);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));

		// The following assertion requires standardness to be checked before allowed script types
		Assert.Equal(WabiSabiProtocolErrorCode.NonStandardOutput, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task NotEnoughFundsAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.MinRegistrableAmount = Money.Coins(2);

		var round = WabiSabiTestFactory.CreateRound(cfg);
		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(Money.Coins(1), round));
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiTestFactory.CreateOutputRegistrationRequest(round);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.NotEnoughFunds, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task TooMuchFundsAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.MaxRegistrableAmount = Money.Coins(1.993m); // TODO migrate to MultipartyTransactionParameters

		var round = WabiSabiTestFactory.CreateRound(cfg);
		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(Money.Coins(2), round));
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiTestFactory.CreateOutputRegistrationRequest(round);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.TooMuchFunds, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task IncorrectRequestedVsizeCredentialsAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		var round = WabiSabiTestFactory.CreateRound(cfg);
		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiTestFactory.CreateOutputRegistrationRequest(round, vsize: 30);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.IncorrectRequestedVsizeCredentials, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task WrongPhaseAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		Round round = WabiSabiTestFactory.CreateRound(cfg);
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));

		// Refresh the Arena States because of vsize manipulation.
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));

		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));

		foreach (Phase phase in Enum.GetValues(typeof(Phase)))
		{
			if (phase != Phase.OutputRegistration)
			{
				var req = WabiSabiTestFactory.CreateOutputRegistrationRequest(round);
				round.SetPhase(phase);
				var ex = await Assert.ThrowsAsync<WrongPhaseException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));
				Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
			}
		}

		await arena.StopAsync(CancellationToken.None);
	}
}
