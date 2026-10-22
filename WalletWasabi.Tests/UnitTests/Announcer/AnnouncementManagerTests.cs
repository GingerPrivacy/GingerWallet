using Moq;
using NNostr.Client;
using NNostr.Client.Protocols;
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
	public async Task StopWaitsForWorkerAndClientDisconnectAsync()
	{
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
		TaskCompletionSource connectStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource allowConnect = new(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource disconnectStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource allowDisconnect = new(TaskCreationOptions.RunContinuationsAsynchronously);
		Mock<INostrClient> client = CreateClient(connectStarted, allowConnect);
		client.Setup(x => x.Disconnect()).Returns(() =>
		{
			disconnectStarted.TrySetResult();
			return allowDisconnect.Task;
		});
		using TestAnnouncementManager manager = new(TestDirectory.Get(), client.Object);
		await manager.StartAsync(CancellationToken.None);
		await connectStarted.Task.WaitAsync(timeout.Token);

		Task stopTask = manager.StopAsync(CancellationToken.None);
		try
		{
			client.Verify(x => x.Disconnect(), Times.Never);
			allowConnect.TrySetResult();
			await disconnectStarted.Task.WaitAsync(timeout.Token);
			Assert.False(stopTask.IsCompleted);
			client.Verify(x => x.Dispose(), Times.Never);
		}
		finally
		{
			allowConnect.TrySetResult();
			allowDisconnect.TrySetResult();
			await stopTask.WaitAsync(timeout.Token);
		}
		client.Verify(x => x.Disconnect(), Times.Once);
		client.Verify(x => x.Dispose(), Times.Once);
	}

	[Fact]
	public async Task StopTimeoutLeavesCleanupWithWorkerAsync()
	{
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
		using CancellationTokenSource stopCancellation = new();
		TaskCompletionSource connectStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource allowConnect = new(TaskCreationOptions.RunContinuationsAsynchronously);
		Mock<INostrClient> client = CreateClient(connectStarted, allowConnect);
		client.Setup(x => x.Disconnect()).Returns(Task.CompletedTask);
		using TestAnnouncementManager manager = new(TestDirectory.Get(), client.Object);
		await manager.StartAsync(CancellationToken.None);
		await connectStarted.Task.WaitAsync(timeout.Token);

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
			allowConnect.TrySetResult();
			await manager.ExecuteTask!.WaitAsync(timeout.Token);
		}
		client.Verify(x => x.Disconnect(), Times.Once);
		client.Verify(x => x.Dispose(), Times.Once);
	}

	private static Mock<INostrClient> CreateClient(TaskCompletionSource started, TaskCompletionSource allowConnect)
	{
		Mock<INostrClient> client = new();
		client.Setup(x => x.ConnectAndWaitUntilConnected(It.IsAny<CancellationToken>())).Returns(() =>
		{
			started.TrySetResult();
			// An in-flight client operation can finish after cancellation is requested.
			return allowConnect.Task;
		});
		client.Setup(x => x.CreateSubscription(It.IsAny<string>(), It.IsAny<NostrSubscriptionFilter[]>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);
		return client;
	}

	private sealed class TestAnnouncementManager(string dataDir, INostrClient client)
		: AnnouncementManager(dataDir, TimeSpan.FromHours(1), DisplayLanguage.English, "", null!, isFirstRun: false)
	{
		protected override INostrClient CreateNostrClient() => client;
	}
}
