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
		}
		finally
		{
			await service.StopAsync(CancellationToken.None);
		}
	}

	[Fact]
	public async Task ReconnectorWaitsForEachNewConnectionAsync()
	{
		using CancellationTokenSource cancellation = new();
		using var service = new GatedReconnector();
		await service.StartAndAwaitReconnectionAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
		Task second = service.StartAndAwaitReconnectionAsync(cancellation.Token);
		try
		{
			Task first = await Task.WhenAny(second, service.SecondAttempt.Task).WaitAsync(TimeSpan.FromSeconds(10));
			Assert.Same(service.SecondAttempt.Task, first);
			Assert.False(second.IsCompleted);
		}
		finally
		{
			cancellation.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second.WaitAsync(TimeSpan.FromSeconds(10)));
			Assert.True(service.ExecuteTask!.IsCompleted);
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
		public TaskCompletionSource SecondAttempt { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			if (Interlocked.Increment(ref _attempt) > 1)
			{
				SecondAttempt.TrySetResult();
				await Task.Delay(Timeout.InfiniteTimeSpan, cancel).ConfigureAwait(false);
			}
			// Simulate a successful transport without opening a socket.
			var success = (TaskCompletionSource<bool>)typeof(P2pReconnector)
				.GetProperty("Success", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this)!;
			success.TrySetResult(true);
		}
	}
}
