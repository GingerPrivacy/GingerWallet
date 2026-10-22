using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinCore.Endpointing;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients;

/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class SingleInstanceCheckerTests
{
	/// <summary>Everything takes longer on CI. Timeouts sane on users' machines are too short for CI.</summary>
	private const int TimeoutMultiplier = 3;

	[Fact]
	public void NetworkToPortUsesUserScope()
	{
		int aliceMainPort = SingleInstanceChecker.NetworkToPort(Network.Main, "alice");
		int aliceMainPortAgain = SingleInstanceChecker.NetworkToPort(Network.Main, "alice");
		int aliceTestNetPort = SingleInstanceChecker.NetworkToPort(Network.TestNet, "alice");
		int aliceRegTestPort = SingleInstanceChecker.NetworkToPort(Network.RegTest, "alice");
		int bobMainPort = SingleInstanceChecker.NetworkToPort(Network.Main, "bob");

		Assert.Equal(aliceMainPort, aliceMainPortAgain);
		Assert.NotEqual(aliceMainPort, aliceTestNetPort);
		Assert.NotEqual(aliceMainPort, aliceRegTestPort);
		Assert.NotEqual(aliceMainPort, bobMainPort);
	}

	[Fact]
	public async Task DefaultNetworkConstructorsUseCurrentUserScopedPortsAsync()
	{
		string userScope = SingleInstanceChecker.GetUserScope();

		Assert.False(string.IsNullOrWhiteSpace(userScope));

		await using SingleInstanceChecker mainNet = new(Network.Main);
		await using SingleInstanceChecker testNet = new(Network.TestNet);
		await using SingleInstanceChecker regTest = new(Network.RegTest);

		Assert.Equal(SingleInstanceChecker.NetworkToPort(Network.Main, userScope), mainNet.Port);
		Assert.Equal(SingleInstanceChecker.NetworkToPort(Network.TestNet, userScope), testNet.Port);
		Assert.Equal(SingleInstanceChecker.NetworkToPort(Network.RegTest, userScope), regTest.Port);

		Assert.NotEqual(mainNet.Port, testNet.Port);
		Assert.NotEqual(mainNet.Port, regTest.Port);
		Assert.NotEqual(testNet.Port, regTest.Port);
	}

	[Fact]
	public async Task InitialInstanceCheckCompletesBeforeWorkerStartsAsync()
	{
		await using DelayedSingleInstanceChecker checker = new(PortFinder.GetRandomPorts(1)[0]);
		try
		{
			var startupTask = checker.CheckSingleInstanceAsync();
			Assert.True(startupTask.IsCompletedSuccessfully);
			Assert.Equal(WasabiInstanceStatus.NoOtherInstanceIsRunning, await startupTask);
			await checker.StopCheckingAsync().WaitAsync(TimeSpan.FromSeconds(10));

			using TcpListener replacementListener = new(IPAddress.Loopback, checker.Port)
			{
				ExclusiveAddressUse = true
			};
			replacementListener.Start();
			Assert.Equal(checker.Port, ((IPEndPoint)replacementListener.LocalEndpoint).Port);
		}
		finally
		{
			checker.AllowListenerStart.TrySetResult();
		}
	}

	[Fact]
	public async Task StopCheckingDoesNotWaitForStartupContinuationAsync()
	{
		using ManualResetEventSlim releaseStartupContinuation = new(false);
		await using DelayedSingleInstanceChecker checker = new(PortFinder.GetRandomPorts(1)[0]);
		TaskCompletionSource startupContinuationEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);

		var startupTask = checker.CheckSingleInstanceAsync();

		// If readiness is asynchronous, subscribe before allowing the worker to
		// publish it. Synchronous readiness can run the blocking caller separately.
		var startupContinuation = startupTask.IsCompleted
			? Task.Run(BlockAfterStartupAsync)
			: BlockAfterStartupAsync();
		try
		{
			checker.AllowListenerStart.TrySetResult();
			await startupContinuationEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			Assert.False(startupContinuation.IsCompleted);

			// The caller may run a blocking GUI loop after startup. Stopping the
			// listener must finish without waiting for that caller to return.
			await checker.StopCheckingAsync().WaitAsync(TimeSpan.FromSeconds(10));
		}
		finally
		{
			releaseStartupContinuation.Set();
			checker.AllowListenerStart.TrySetResult();
			await startupContinuation.WaitAsync(TimeSpan.FromSeconds(10));
		}

		async Task BlockAfterStartupAsync()
		{
			var status = await startupTask.ConfigureAwait(false);
			Assert.Equal(WasabiInstanceStatus.NoOtherInstanceIsRunning, status);
			startupContinuationEntered.TrySetResult();
			releaseStartupContinuation.Wait();
		}
	}

	[Fact]
	public async Task SingleInstanceTestsAsync()
	{
		var ports = PortFinder.GetRandomPorts(3);
		int mainNetPort = ports[0];
		int testNetPort = ports[1];
		int regTestPort = ports[2];

		// Disposal test.
		await using (SingleInstanceChecker sic = new(mainNetPort, TimeoutMultiplier))
		{
			await sic.CheckSingleInstanceAsync();
		}

		// Check different networks.
		await using SingleInstanceChecker sicMainNet = new(mainNetPort, TimeoutMultiplier);
		var status = await sicMainNet.CheckSingleInstanceAsync();
		Assert.Equal(WasabiInstanceStatus.NoOtherInstanceIsRunning, status);

		await using SingleInstanceChecker sicMainNet2 = new(mainNetPort, TimeoutMultiplier);
		status = await sicMainNet2.CheckSingleInstanceAsync();
		Assert.Equal(WasabiInstanceStatus.AnotherInstanceIsRunning, status);

		// testnet
		await using SingleInstanceChecker sicTestNet1 = new(testNetPort, TimeoutMultiplier);
		status = await sicTestNet1.CheckSingleInstanceAsync();
		Assert.Equal(WasabiInstanceStatus.NoOtherInstanceIsRunning, status);

		await using SingleInstanceChecker sicTestNet2 = new(testNetPort, TimeoutMultiplier);
		status = await sicTestNet2.CheckSingleInstanceAsync();
		Assert.Equal(WasabiInstanceStatus.AnotherInstanceIsRunning, status);

		// regtest
		await using SingleInstanceChecker sicRegNet1 = new(regTestPort, TimeoutMultiplier);
		status = await sicRegNet1.CheckSingleInstanceAsync();
		Assert.Equal(WasabiInstanceStatus.NoOtherInstanceIsRunning, status);

		await using SingleInstanceChecker sicRegNet2 = new(regTestPort, TimeoutMultiplier);
		status = await sicRegNet2.CheckSingleInstanceAsync();
		Assert.Equal(WasabiInstanceStatus.AnotherInstanceIsRunning, status);
	}

	[Fact]
	public async Task OtherInstanceStartedTestsAsync()
	{
		int mainNetPort = PortFinder.GetRandomPorts(1)[0];

		// Disposal test.
		await using SingleInstanceChecker firstInstance = new(mainNetPort, TimeoutMultiplier);
		long eventCalled = 0;

		firstInstance.OtherInstanceStarted += SetCalled;

		try
		{
			// I am the first instance this should be fine.
			var status = await firstInstance.CheckSingleInstanceAsync();
			Assert.Equal(WasabiInstanceStatus.NoOtherInstanceIsRunning, status);

			await using SingleInstanceChecker secondInstance = new(mainNetPort, TimeoutMultiplier);

			for (int i = 0; i < 2; i++)
			{
				// I am the second one.
				var secondInstanceStatus = await secondInstance.CheckSingleInstanceAsync();
				Assert.Equal(WasabiInstanceStatus.AnotherInstanceIsRunning, secondInstanceStatus);
			}

			// Overall Timeout.
			using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));

			// Simulate a port scan operation.
			using (TcpClient client = new()
			{
				NoDelay = true
			})
			{
				// This should not be counted.
				await client.ConnectAsync(IPAddress.Loopback, mainNetPort, cts.Token);
				await using NetworkStream networkStream = client.GetStream();
				networkStream.WriteTimeout = (int)SingleInstanceChecker.ClientTimeOut.TotalMilliseconds;
				await using var writer = new StreamWriter(networkStream, Encoding.UTF8);
				await writer.WriteAsync(new StringBuilder("fake message"), cts.Token);
				await writer.FlushAsync().WaitAsync(cts.Token);
				await networkStream.FlushAsync(cts.Token);
			}

			// Simulate a port scan operation.
			try
			{
				using TcpClient client = new()
				{
					NoDelay = true
				};

				// This should not be counted.
				await client.ConnectAsync(IPAddress.Loopback, mainNetPort, cts.Token);
				await using NetworkStream networkStream = client.GetStream();

				// This should throw as the first instance should disconnect the clients after the timeout.
				await using var writer = new StreamWriter(networkStream, Encoding.UTF8);

				// Wait until timeout on Server side, so the client will be disconnected.
				await Task.Delay(SingleInstanceChecker.ClientTimeOut + TimeSpan.FromMilliseconds(500), cts.Token);

				// This won't throw if the connection is lost, just continues.
				await writer.WriteAsync(new StringBuilder("late message"), cts.Token);
				await writer.FlushAsync().WaitAsync(cts.Token);
				await networkStream.FlushAsync(cts.Token);
			}
			catch (IOException)
			{
				// If the underlying connection is lost and there is something in the send buffer NetworkStream dispose will throw.
			}

			// One more to check if the first instance was able to recover from the port scan operation.
			var statusAfterPortScan = await secondInstance.CheckSingleInstanceAsync();
			Assert.Equal(WasabiInstanceStatus.AnotherInstanceIsRunning, statusAfterPortScan);

			while (Interlocked.Read(ref eventCalled) != 3)
			{
				cts.Token.ThrowIfCancellationRequested();
			}

			// There should be the same number of events as the number of tries from the second instance.
			Assert.Equal(3, Interlocked.Read(ref eventCalled));
		}
		finally
		{
			firstInstance.OtherInstanceStarted -= SetCalled;
		}

		void SetCalled(object? sender, EventArgs args)
		{
			Interlocked.Increment(ref eventCalled);
		}
	}

	private sealed class DelayedSingleInstanceChecker(int port) : SingleInstanceChecker(port, TimeoutMultiplier)
	{
		public TaskCompletionSource AllowListenerStart { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await AllowListenerStart.Task.WaitAsync(stoppingToken).ConfigureAwait(false);
			await base.ExecuteAsync(stoppingToken).ConfigureAwait(false);
		}
	}
}
