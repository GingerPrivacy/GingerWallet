using NBitcoin;
using NBitcoin.Protocol;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Stores;
using WalletWasabi.Tests.TestCommon;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinP2p;

public class P2pNetworkTests
{
	[Fact]
	public async Task StopDuringStartupDisconnectsNodesAsync()
	{
		BitcoinStore bitcoinStore = new(null!, null!, new MempoolService(), null!, null!);
		using var network = new DelayedP2pNetwork(TestDirectory.Get(), bitcoinStore);
		// Exercise the real NodesGroup lifecycle without starting any peer connections.
		network.Nodes.MaximumNodeConnection = 0;
		await network.StartAsync(CancellationToken.None);
		await network.WorkerStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

		await network.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

		// NodesGroup has no public connection-loop status; Disconnect cancels this source.
		var disconnect = Assert.IsType<CancellationTokenSource>(typeof(NodesGroup)
			.GetField("_Disconnect", BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(network.Nodes));
		Assert.True(disconnect.IsCancellationRequested);
	}

	private sealed class DelayedP2pNetwork(string workDir, BitcoinStore bitcoinStore)
		: P2pNetwork(Network.Main, new IPEndPoint(IPAddress.Loopback, 1), null, workDir, bitcoinStore)
	{
		public TaskCompletionSource WorkerStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			WorkerStarted.SetResult();
			// Hold initialization until StopAsync requests cancellation, reproducing a delayed worker.
			await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
			await base.ExecuteAsync(stoppingToken).ConfigureAwait(false);
		}
	}
}
