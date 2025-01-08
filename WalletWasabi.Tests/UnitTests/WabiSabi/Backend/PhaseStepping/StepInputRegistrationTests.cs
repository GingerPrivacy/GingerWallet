using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.WabiSabi.Backend.Rounds.Utils;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Rounds;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PhaseStepping;

public class StepInputRegistrationTests
{
	[Fact]
	public async Task RoundFullAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.MaxInputCountByRound = 3;

		var round = WabiSabiTestFactory.CreateRound(cfg);
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.InputRegistration, round.Phase);

		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.InputRegistration, round.Phase);

		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DetectSpentTxoBeforeSteppingIntoConnectionConfirmationAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.MaxInputCountByRound = 3;

		var round = WabiSabiTestFactory.CreateRound(cfg);
		var offendingAlice = WabiSabiTestFactory.CreateAlice(round); // this Alice spent the coin after registration

		var mockRpc = WabiSabiTestFactory.CreatePreconfiguredRpcClient();
		var defaultBehavior = mockRpc.OnGetTxOutAsync;
		mockRpc.OnGetTxOutAsync = (txId, n, b) =>
		{
			var outpoint = offendingAlice.Coin.Outpoint;
			if ((txId, n) == (outpoint.Hash, outpoint.N))
			{
				return null;
			}

			return defaultBehavior?.Invoke(txId, n, b);
		};

		using Arena arena = await ArenaTestFactory.From(cfg).With(mockRpc).CreateAndStartAsync(round);

		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		round.Alices.Add(offendingAlice);
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.InputRegistration, round.Phase);
		Assert.Equal(2, round.Alices.Count); // the offending alice was removed

		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.ConnectionConfirmation, round.Phase);
		Assert.Equal(3, round.Alices.Count);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task BlameRoundFullAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.MaxInputCountByRound = 4;
		cfg.MinInputCountByRoundMultiplier = 0.5;

		var round = WabiSabiTestFactory.CreateRound(cfg);
		var alice1 = WabiSabiTestFactory.CreateAlice(round);
		var alice2 = WabiSabiTestFactory.CreateAlice(round);
		var alice3 = WabiSabiTestFactory.CreateAlice(round);
		round.Alices.Add(alice1);
		round.Alices.Add(alice2);
		round.Alices.Add(alice3);
		var blameRound = WabiSabiTestFactory.CreateBlameRound(round, cfg);

		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(blameRound);

		blameRound.Alices.Add(alice1);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.InputRegistration, blameRound.Phase);

		blameRound.Alices.Add(alice2);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.InputRegistration, blameRound.Phase);

		blameRound.Alices.Add(alice3);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.ConnectionConfirmation, blameRound.Phase);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputRegistrationTimedoutWithSufficientInputsAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.StandardInputRegistrationTimeout = TimeSpan.Zero;
		cfg.MaxInputCountByRound = 4;
		cfg.MinInputCountByRoundMultiplier = 0.5;

		var round = WabiSabiTestFactory.CreateRound(cfg);
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));

		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task BlameRoundTimedoutWithSufficientInputsAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.BlameInputRegistrationTimeout = TimeSpan.Zero;
		cfg.StandardInputRegistrationTimeout = TimeSpan.FromHours(1); // Test that this is disregarded.
		cfg.MaxInputCountByRound = 4;
		cfg.MinInputCountByRoundMultiplier = 0.5;

		var round = WabiSabiTestFactory.CreateRound(cfg);
		var alice1 = WabiSabiTestFactory.CreateAlice(round);
		var alice2 = WabiSabiTestFactory.CreateAlice(round);
		var alice3 = WabiSabiTestFactory.CreateAlice(round);
		round.Alices.Add(alice1);
		round.Alices.Add(alice2);
		round.Alices.Add(alice3);
		var blameRound = WabiSabiTestFactory.CreateBlameRound(round, cfg);
		blameRound.Alices.Add(alice1);
		blameRound.Alices.Add(alice2);

		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(blameRound);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.ConnectionConfirmation, blameRound.Phase);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputRegistrationTimedoutWithoutSufficientInputsAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.StandardInputRegistrationTimeout = TimeSpan.Zero;
		cfg.MaxInputCountByRound = 4;
		cfg.MinInputCountByRoundMultiplier = 0.5;

		var round = WabiSabiTestFactory.CreateRound(cfg);
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.Ended, round.Phase);
		Assert.DoesNotContain(round, arena.GetActiveRounds());

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task BlameRoundTimedoutWithoutSufficientInputsAsync()
	{
		// This test also tests that the min input count multiplier is applied
		// against the max input count by round number and not against the
		// number of inputs awaited by the blame round itself.
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.BlameInputRegistrationTimeout = TimeSpan.Zero;
		cfg.StandardInputRegistrationTimeout = TimeSpan.FromHours(1); // Test that this is disregarded.
		cfg.MaxInputCountByRound = 10;
		cfg.MinInputCountByRoundMultiplier = 0.5;
		cfg.MinInputCountByBlameRoundMultiplier = 0.4;

		var round = WabiSabiTestFactory.CreateRound(cfg);
		var alice1 = WabiSabiTestFactory.CreateAlice(round);
		var alice2 = WabiSabiTestFactory.CreateAlice(round);
		var alice3 = WabiSabiTestFactory.CreateAlice(round);
		var alice4 = WabiSabiTestFactory.CreateAlice(round);
		var alice5 = WabiSabiTestFactory.CreateAlice(round);
		round.Alices.Add(alice1);
		round.Alices.Add(alice2);
		round.Alices.Add(alice3);
		round.Alices.Add(alice4);
		round.Alices.Add(alice5);
		var blameRound = WabiSabiTestFactory.CreateBlameRound(round, cfg);
		blameRound.Alices.Add(alice1);
		blameRound.Alices.Add(alice2);
		blameRound.Alices.Add(alice3);

		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(blameRound);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.Ended, blameRound.Phase);
		Assert.DoesNotContain(blameRound, arena.GetActiveRounds());

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputRegistrationTimeoutCanBeModifiedRuntimeAsync()
	{
		WabiSabiConfig cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.StandardInputRegistrationTimeout = TimeSpan.FromHours(1);
		cfg.MaxInputCountByRound = 4;
		cfg.MinInputCountByRoundMultiplier = 0.5;

		var round = WabiSabiTestFactory.CreateRound(cfg);
		using Arena arena = await ArenaTestFactory.From(cfg).CreateAndStartAsync(round);

		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		round.Alices.Add(WabiSabiTestFactory.CreateAlice(round));
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.InputRegistration, round.Phase);

		round.InputRegistrationTimeFrame = round.InputRegistrationTimeFrame with { Duration = TimeSpan.Zero };

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

		await arena.StopAsync(CancellationToken.None);
	}
}
