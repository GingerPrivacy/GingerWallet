using Moq;
using NNostr.Client;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Announcer;
using WalletWasabi.Models;
using WalletWasabi.Tests.TestCommon;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Announcer;

public class AnnouncementManagerTests
{
	[Fact]
	public async Task RestartDisposesEachRunsClientOnceAsync()
	{
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
		using RestartableAnnouncementManager manager = new(TestDirectory.Get());
		Mock<INostrClient> firstClient = new();
		firstClient.Setup(x => x.Disconnect()).Returns(Task.CompletedTask);
		Mock<INostrClient> secondClient = new();
		secondClient.Setup(x => x.Disconnect()).Returns(Task.CompletedTask);

		foreach (var client in new[] { firstClient, secondClient })
		{
			var run = manager.QueueRun(client.Object);
			await manager.StartAsync(CancellationToken.None);
			await run.Started.Task.WaitAsync(timeout.Token);
			Task stopTask = manager.StopAsync(CancellationToken.None);
			run.AllowExit.TrySetResult();
			await stopTask.WaitAsync(timeout.Token);
			client.Verify(x => x.Disconnect(), Times.Once);
			client.Verify(x => x.Dispose(), Times.Once);
		}
		firstClient.Verify(x => x.Dispose(), Times.Once);
	}

	[Fact]
	public async Task RestartWaitsForCleanupAfterStopTimeoutAsync()
	{
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
		using CancellationTokenSource stopCancellation = new();
		TaskCompletionSource allowDisconnect = new(TaskCreationOptions.RunContinuationsAsynchronously);
		Mock<INostrClient> firstClient = new();
		firstClient.Setup(x => x.Disconnect()).Returns(allowDisconnect.Task);
		Mock<INostrClient> secondClient = new();
		secondClient.Setup(x => x.Disconnect()).Returns(Task.CompletedTask);
		using RestartableAnnouncementManager manager = new(TestDirectory.Get());
		var firstRun = manager.QueueRun(firstClient.Object);
		var secondRun = manager.QueueRun(secondClient.Object);
		await manager.StartAsync(CancellationToken.None);
		await firstRun.Started.Task.WaitAsync(timeout.Token);
		Task firstWorker = manager.ExecuteTask!;
		Task stopTask = manager.StopAsync(stopCancellation.Token);
		stopCancellation.Cancel();
		await stopTask.WaitAsync(timeout.Token);

		Task restartTask = manager.StartAsync(CancellationToken.None);
		try
		{
			Assert.False(restartTask.IsCompleted);
			Assert.False(secondRun.Started.Task.IsCompleted);
			firstClient.Verify(x => x.Dispose(), Times.Never);
		}
		finally
		{
			firstRun.AllowExit.TrySetResult();
			allowDisconnect.TrySetResult();
			await firstWorker.WaitAsync(timeout.Token);
			await restartTask.WaitAsync(timeout.Token);
			await secondRun.Started.Task.WaitAsync(timeout.Token);
			secondRun.AllowExit.TrySetResult();
			await manager.StopAsync(CancellationToken.None).WaitAsync(timeout.Token);
		}
		firstClient.Verify(x => x.Dispose(), Times.Once);
		secondClient.Verify(x => x.Dispose(), Times.Once);
	}

	[Fact]
	public async Task StopWaitsForWorkerBeforeDisposingClientAsync()
	{
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
		Mock<INostrClient> client = new();
		client.Setup(x => x.Disconnect()).Returns(Task.CompletedTask);
		using GatedAnnouncementManager manager = new(TestDirectory.Get());
		SetClient(manager, client.Object);
		await manager.StartAsync(CancellationToken.None);
		await manager.WorkerStarted.Task.WaitAsync(timeout.Token);

		Task stopTask = manager.StopAsync(CancellationToken.None);
		try
		{
			client.Verify(x => x.Disconnect(), Times.Never);
			client.Verify(x => x.Dispose(), Times.Never);
			Assert.False(stopTask.IsCompleted);
		}
		finally
		{
			manager.AllowWorkerExit.TrySetResult();
			await stopTask.WaitAsync(timeout.Token);
		}
		client.Verify(x => x.Disconnect(), Times.Once);
		client.Verify(x => x.Dispose(), Times.Once);
	}

	[Fact]
	public async Task StopDisposesClientCreatedDuringShutdownAsync()
	{
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
		Mock<INostrClient> client = new();
		client.Setup(x => x.Disconnect()).Returns(Task.CompletedTask);
		using GatedAnnouncementManager manager = new(TestDirectory.Get());
		manager.BeforeWorkerExit = () => SetClient(manager, client.Object);
		await manager.StartAsync(CancellationToken.None);
		await manager.WorkerStarted.Task.WaitAsync(timeout.Token);

		Task stopTask = manager.StopAsync(CancellationToken.None);
		manager.AllowWorkerExit.TrySetResult();
		await stopTask.WaitAsync(timeout.Token);
		client.Verify(x => x.Disconnect(), Times.Once);
		client.Verify(x => x.Dispose(), Times.Once);
	}

	[Fact]
	public async Task StopAwaitsDisconnectBeforeDisposingClientAsync()
	{
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
		TaskCompletionSource disconnectStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource allowDisconnect = new(TaskCreationOptions.RunContinuationsAsynchronously);
		Mock<INostrClient> client = new();
		client.Setup(x => x.Disconnect()).Returns(() =>
		{
			disconnectStarted.TrySetResult();
			return allowDisconnect.Task;
		});
		using GatedAnnouncementManager manager = new(TestDirectory.Get());
		SetClient(manager, client.Object);

		Task stopTask = manager.StopAsync(CancellationToken.None);
		try
		{
			await disconnectStarted.Task.WaitAsync(timeout.Token);
			client.Verify(x => x.Dispose(), Times.Never);
			Assert.False(stopTask.IsCompleted);
		}
		finally
		{
			allowDisconnect.TrySetResult();
			await stopTask.WaitAsync(timeout.Token);
		}
		client.Verify(x => x.Dispose(), Times.Once);
	}

	[Fact]
	public async Task StopTimeoutDoesNotDisposeClientWhileWorkerIsRunningAsync()
	{
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
		using CancellationTokenSource stopCancellation = new();
		TaskCompletionSource clientDisposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
		Mock<INostrClient> client = new();
		client.Setup(x => x.Disconnect()).Returns(Task.CompletedTask);
		client.Setup(x => x.Dispose()).Callback(() => clientDisposed.TrySetResult());
		using GatedAnnouncementManager manager = new(TestDirectory.Get());
		SetClient(manager, client.Object);
		await manager.StartAsync(CancellationToken.None);
		await manager.WorkerStarted.Task.WaitAsync(timeout.Token);

		Task stopTask = manager.StopAsync(stopCancellation.Token);
		stopCancellation.Cancel();
		try
		{
			await stopTask.WaitAsync(timeout.Token);
			client.Verify(x => x.Disconnect(), Times.Never);
			client.Verify(x => x.Dispose(), Times.Never);
		}
		finally
		{
			manager.AllowWorkerExit.TrySetResult();
			await manager.ExecuteTask!.WaitAsync(timeout.Token);
		}
		await clientDisposed.Task.WaitAsync(timeout.Token);
		client.Verify(x => x.Dispose(), Times.Once);
	}

	private static void SetClient(AnnouncementManager manager, INostrClient client)
	{
		// Install a fake client without opening a real relay connection. The public
		// constructor currently creates the Nostr client internally on the first tick.
		typeof(AnnouncementManager).GetField("_nostrClient", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, client);
	}

	private sealed class GatedAnnouncementManager(string dataDir)
		: AnnouncementManager(dataDir, TimeSpan.FromHours(1), DisplayLanguage.English, "", null!, isFirstRun: false)
	{
		public TaskCompletionSource WorkerStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource AllowWorkerExit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public Action? BeforeWorkerExit { get; set; }

		protected override async Task ActionAsync(CancellationToken cancellationToken)
		{
			WorkerStarted.TrySetResult();
			await AllowWorkerExit.Task;
			BeforeWorkerExit?.Invoke();
		}
	}

	private sealed class RestartableAnnouncementManager(string dataDir)
		: AnnouncementManager(dataDir, TimeSpan.FromHours(1), DisplayLanguage.English, "", null!, isFirstRun: false)
	{
		private readonly Queue<Run> _runs = new();

		public Run QueueRun(INostrClient client)
		{
			Run run = new(client);
			lock (_runs)
			{
				_runs.Enqueue(run);
			}
			return run;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			Run run;
			lock (_runs)
			{
				run = _runs.Dequeue();
			}
			SetClient(this, run.Client);
			run.Started.TrySetResult();
			await run.AllowExit.Task;
		}

		public sealed class Run(INostrClient client)
		{
			public INostrClient Client { get; } = client;
			public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
			public TaskCompletionSource AllowExit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		}
	}
}
