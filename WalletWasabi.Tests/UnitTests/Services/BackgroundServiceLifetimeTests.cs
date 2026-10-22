using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Daemon.BuySell;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class BackgroundServiceLifetimeTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SleepInhibitorReleasesPowerRequestAsync(bool dispose)
	{
		var powerRequest = new TrackingPowerRequest();
		using SleepInhibitor service = new(() => CoinJoinClientState.InProgress, () => Task.FromResult<IPowerSavingInhibitorTask>(powerRequest));
		TaskCompletionSource tick = new(TaskCreationOptions.RunContinuationsAsynchronously);
		service.Tick += (_, _) => tick.TrySetResult();
		await service.StartAsync(CancellationToken.None);
		await tick.Task.WaitAsync(TimeSpan.FromSeconds(10));
		if (dispose)
		{
			service.Dispose();
		}
		else
		{
			await service.StopAsync(CancellationToken.None);
		}
		await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
		Assert.Equal(1, powerRequest.StopCalls);
	}

	[Fact]
	public async Task SleepInhibitorStopsRequestCreatedDuringShutdownAsync()
	{
		TaskCompletionSource factoryEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource<IPowerSavingInhibitorTask> factoryResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
		var powerRequest = new TrackingPowerRequest();
		using SleepInhibitor service = new(() => CoinJoinClientState.InProgress, () =>
		{
			factoryEntered.SetResult();
			return factoryResult.Task;
		});
		await service.StartAsync(CancellationToken.None);
		await factoryEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
		Task stop = service.StopAsync(CancellationToken.None);
		Assert.False(stop.IsCompleted);
		factoryResult.SetResult(powerRequest);
		await stop.WaitAsync(TimeSpan.FromSeconds(10));
		Assert.Equal(1, powerRequest.StopCalls);
	}

	[Fact]
	public async Task BuySellDisposeCancelsPeriodicWorkerAsync()
	{
		var service = new WaitingBuySellManager();
		try
		{
			await service.StartAsync(CancellationToken.None);
			await service.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			service.Dispose();
			Assert.True(service.WorkerToken.IsCancellationRequested);
			Assert.Null(Record.Exception(service.Dispose));
		}
		finally
		{
			await service.StopAsync(CancellationToken.None);
		}
	}

	[Fact]
	public async Task ReconnectorWaitsForEachNewConnectionAsync()
	{
		using var service = new GatedReconnector();
		await service.StartAndAwaitReconnectionAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
		Task second = service.StartAndAwaitReconnectionAsync(CancellationToken.None);
		try
		{
			Task first = await Task.WhenAny(second, service.SecondAttempt.Task).WaitAsync(TimeSpan.FromSeconds(10));
			Assert.Same(service.SecondAttempt.Task, first);
			Assert.False(service.SecondToken.IsCancellationRequested);
			Assert.False(second.IsCompleted);
		}
		finally
		{
			service.AllowSecond.TrySetResult();
			await second.WaitAsync(TimeSpan.FromSeconds(10));
		}
	}

	[Fact]
	public async Task ReconnectorStopReleasesPendingWaiterAsync()
	{
		using var service = new GatedReconnector();
		await service.StartAndAwaitReconnectionAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
		Task reconnect = service.StartAndAwaitReconnectionAsync(CancellationToken.None);
		await service.SecondAttempt.Task.WaitAsync(TimeSpan.FromSeconds(10));
		await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reconnect.WaitAsync(TimeSpan.FromSeconds(10)));
	}

	[Fact]
	public async Task ReconnectorCancellationAfterSuccessDoesNotThrowAsync()
	{
		using CancellationTokenSource cancellation = new();
		using var service = new GatedReconnector { DelayStop = true };
		Task reconnect = service.StartAndAwaitReconnectionAsync(cancellation.Token);
		try
		{
			await service.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			Assert.Null(Record.Exception(() => cancellation.Cancel()));
		}
		finally
		{
			service.AllowStop.TrySetResult();
			await reconnect.WaitAsync(TimeSpan.FromSeconds(10));
		}
	}

	private sealed class TrackingPowerRequest : IPowerSavingInhibitorTask
	{
		public int StopCalls { get; private set; }
		public bool IsDone => StopCalls > 0;
		public bool Prolong(TimeSpan timeSpan) => !IsDone;
		public Task StopAsync()
		{
			StopCalls++;
			return Task.CompletedTask;
		}
	}

	private sealed class WaitingBuySellManager() : BuySellManager(null!, null!, Timeout.InfiniteTimeSpan)
	{
		public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public CancellationToken WorkerToken { get; private set; }
		protected override async Task ActionAsync(CancellationToken cancel)
		{
			WorkerToken = cancel;
			Entered.TrySetResult();
			await Task.Delay(Timeout.InfiniteTimeSpan, cancel).ConfigureAwait(false);
		}
	}

	private sealed class GatedReconnector() : P2pReconnector(TimeSpan.FromSeconds(5), new P2pNode(Network.RegTest, new IPEndPoint(IPAddress.Loopback, 1), new MempoolService(), "/lifecycle-tests/"))
	{
		private int _attempt;
		public bool DelayStop { get; init; }
		public CancellationToken SecondToken { get; private set; }
		public TaskCompletionSource SecondAttempt { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource AllowSecond { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource StopEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource AllowStop { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			if (Interlocked.Increment(ref _attempt) > 1)
			{
				SecondToken = cancel;
				SecondAttempt.TrySetResult();
				await AllowSecond.Task.WaitAsync(cancel).ConfigureAwait(false);
			}
			// Simulate a successful transport without opening a socket.
			var success = (TaskCompletionSource<bool>)typeof(P2pReconnector)
				.GetProperty("Success", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this)!;
			success.TrySetResult(true);
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			if (DelayStop)
			{
				StopEntered.TrySetResult();
				await AllowStop.Task.ConfigureAwait(false);
			}
			await base.StopAsync(cancellationToken).ConfigureAwait(false);
		}
	}
}
