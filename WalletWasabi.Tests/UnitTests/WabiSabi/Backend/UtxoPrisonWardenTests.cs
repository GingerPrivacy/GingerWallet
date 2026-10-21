using Moq;
using NBitcoin;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend.DoSPrevention;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class UtxoPrisonWardenTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CanStartAndStopAsync(bool cancelStartup)
	{
		var workDir = TestDirectory.Get();
		CoordinatorParameters coordinatorParameters = new(workDir);
		using var w = new Warden(
			coordinatorParameters.PrisonFilePath,
			WabiSabiTestFactory.CreateCoinJoinIdStore(),
			coordinatorParameters.RuntimeCoordinatorConfig);
		using CancellationTokenSource startupCts = new();
		if (cancelStartup)
		{
			startupCts.Cancel();
		}
		await w.StartAsync(startupCts.Token);
		await w.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task StopDrainsPrisonBeforeWorkerStartsAsync()
	{
		CoordinatorParameters parameters = new(TestDirectory.Get());
		using var warden = new DelayedWarden(parameters);
		await warden.StartAsync(CancellationToken.None);
		var outPoint = BitcoinFactory.CreateOutPoint(TestRandom.Get());
		warden.Prison.CheatingDetected(outPoint, uint256.One);

		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
		var stopTask = warden.StopAsync(timeout.Token);
		try
		{
			Assert.False(stopTask.IsCompleted);
		}
		finally
		{
			warden.AllowExecution.TrySetResult();
			await stopTask;
		}

		var line = Assert.Single(File.ReadAllLines(parameters.PrisonFilePath));
		Assert.Equal(outPoint, Offender.FromStringLine(line).OutPoint);
	}

	[Fact]
	public async Task PrisonSerializationAsync()
	{
		var rnd = TestRandom.Get();
		var workDir = TestDirectory.Get();

		var i1 = BitcoinFactory.CreateOutPoint(rnd);
		var i2 = BitcoinFactory.CreateOutPoint(rnd);
		var i3 = BitcoinFactory.CreateOutPoint(rnd);
		var i4 = BitcoinFactory.CreateOutPoint(rnd);
		var i5 = BitcoinFactory.CreateOutPoint(rnd);
		var i6 = BitcoinFactory.CreateOutPoint(rnd);

		// Create prison.
		{
			CoordinatorParameters coordinatorParameters = new(workDir);
			using var w = new Warden(
				coordinatorParameters.PrisonFilePath,
				WabiSabiTestFactory.CreateCoinJoinIdStore(),
				coordinatorParameters.RuntimeCoordinatorConfig);
			await w.StartAsync(CancellationToken.None);
			var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
			w.Prison.FailedVerification(i1, uint256.One, TimeSpan.Zero, "local"); // For local we will add a special reason.
			w.Prison.FailedToConfirm(i2, Money.Coins(0.01m), uint256.One);
			w.Prison.FailedToSign(i3, Money.Coins(0.1m), uint256.One);
			w.Prison.DoubleSpent(i4, Money.Coins(0.1m), uint256.One);
			w.Prison.CheatingDetected(i5, uint256.One);
			w.Prison.InheritPunishment(i6, [i3], [InputBannedReasonEnum.RoundDisruptionMethodDidNotSignalReadyToSign]);

			// Wait until serializes.
			await w.StopAsync(CancellationToken.None);
		}

		// See if prev UTXOs are loaded.
		{
			CoordinatorParameters coordinatorParameters2 = new(workDir);
			var coinjoinIdStoreMock = new Mock<ICoinJoinIdStore>();
			coinjoinIdStoreMock.Setup(x => x.Contains(It.IsAny<uint256>())).Returns(true);
			using var w2 = new Warden(coordinatorParameters2.PrisonFilePath, coinjoinIdStoreMock.Object, coordinatorParameters2.RuntimeCoordinatorConfig);
			await w2.StartAsync(CancellationToken.None);

			var dosConfig = coordinatorParameters2.RuntimeCoordinatorConfig.GetDoSConfiguration();
			Assert.True(w2.Prison.IsBanned(i1, dosConfig, DateTimeOffset.UtcNow));
			Assert.True(w2.Prison.IsBanned(i2, dosConfig, DateTimeOffset.UtcNow));
			Assert.True(w2.Prison.IsBanned(i3, dosConfig, DateTimeOffset.UtcNow));
			Assert.True(w2.Prison.IsBanned(i4, dosConfig, DateTimeOffset.UtcNow));
			Assert.True(w2.Prison.IsBanned(i5, dosConfig, DateTimeOffset.UtcNow));
			Assert.True(w2.Prison.IsBanned(i6, dosConfig, DateTimeOffset.UtcNow));

			Assert.Equal(InputBannedReasonEnum.LocalCoinVerifier, w2.Prison.GetBan(i1, dosConfig).Reasons.First());

			Assert.Equal([InputBannedReasonEnum.Inherited, InputBannedReasonEnum.RoundDisruptionMethodDidNotSignalReadyToSign], w2.Prison.GetBan(i6, dosConfig).Reasons.OrderBy(x => x));

			await w2.StopAsync(CancellationToken.None);
		}
	}

	private sealed class DelayedWarden(CoordinatorParameters parameters)
		: Warden(parameters.PrisonFilePath, WabiSabiTestFactory.CreateCoinJoinIdStore(), parameters.RuntimeCoordinatorConfig)
	{
		public TaskCompletionSource AllowExecution { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await AllowExecution.Task.WaitAsync(stoppingToken);
			await base.ExecuteAsync(stoppingToken);
		}
	}
}
