using NBitcoin;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Mempool;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore;

/// <summary>
/// Tests for <see cref="MempoolMirror"/>.
/// </summary>
public class MempoolMirrorTests
{
	[Fact]
	public async Task RetriesAfterInitialRpcFailureAsync()
	{
		TaskCompletionSource firstRequestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource<uint256[]> firstResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource successfulRound = new(TaskCreationOptions.RunContinuationsAsynchronously);
		Transaction transaction = Network.RegTest.CreateTransaction();
		int requests = 0;
		Mock<IRPCClient> rpc = new(MockBehavior.Strict);
		rpc.Setup(x => x.GetRawMempoolAsync(It.IsAny<CancellationToken>())).Returns(() =>
		{
			if (Interlocked.Increment(ref requests) == 1)
			{
				firstRequestStarted.TrySetResult();
				return firstResponse.Task;
			}
			return Task.FromResult(new[] { transaction.GetHash() });
		});
		rpc.Setup(x => x.GetRawTransactionsAsync(It.IsAny<IEnumerable<uint256>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new[] { transaction });
		P2pNode node = new(Network.RegTest, new IPEndPoint(IPAddress.Loopback, 1), new MempoolService(), "/shutdown-test/");
		using MempoolMirror mirror = new(TimeSpan.FromMilliseconds(20), rpc.Object, node);
		mirror.Tick += (_, _) => successfulRound.TrySetResult();
		try
		{
			await mirror.StartAsync(CancellationToken.None);
			await firstRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
			firstResponse.SetException(new IOException("The first RPC request failed."));
			await successfulRound.Task.WaitAsync(TimeSpan.FromSeconds(5));
			Assert.Contains(transaction.GetHash(), mirror.GetMempoolHashes());
			Assert.False(mirror.ExecuteTask!.IsCompleted);
		}
		finally
		{
			firstResponse.TrySetCanceled();
			await mirror.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
			await node.DisposeAsync();
		}
	}

	[Fact]
	public async Task CanCopyMempoolFromRpcAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync();
		using HostedServices services = new();
		services.Register<MempoolMirror>(() => new MempoolMirror(TimeSpan.FromSeconds(2), coreNode.RpcClient, coreNode.P2pNode), "Mempool Mirror");
		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;
			var mempoolInstance = services.Get<MempoolMirror>();
			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			var spendAmount = new Money(0.0004m, MoneyUnit.BTC);
			var spendAmount2 = new Money(0.0004m, MoneyUnit.BTC);
			await rpc.GenerateAsync(101);

			var txid = await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), spendAmount);
			var txid2 = await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), spendAmount2);

			while ((await rpc.GetRawMempoolAsync()).Length != 2)
			{
				await Task.Delay(50);
			}

			await services.StartAllAsync();
			await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));
			var localMempoolHashes = mempoolInstance.GetMempoolHashes();

			Assert.Equal(2, localMempoolHashes.Count);
			Assert.Contains(txid, localMempoolHashes);
			Assert.Contains(txid2, localMempoolHashes);
		}
		finally
		{
			await services.StopAllAsync();
			await coreNode.TryStopAsync(true, 2);
		}
	}

	[Fact]
	public async Task CanHandleArrivingTxAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync();
		using HostedServices services = new();
		services.Register<MempoolMirror>(() => new MempoolMirror(TimeSpan.FromSeconds(7), coreNode.RpcClient, coreNode.P2pNode), "Mempool Mirror");
		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;
			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			await services.StartAllAsync();

			await rpc.GenerateAsync(101);

			var txid = await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), new Money(0.0004m, MoneyUnit.BTC));

			while ((await rpc.GetRawMempoolAsync()).Length == 0)
			{
				await Task.Delay(50);
			}

			var mempoolInstance = services.Get<MempoolMirror>();
			await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			var localMempoolHashes = mempoolInstance.GetMempoolHashes();

			Assert.Single(localMempoolHashes);
			Assert.Contains(txid, localMempoolHashes);
		}
		finally
		{
			await services.StopAllAsync();
			await coreNode.TryStopAsync(true, 2);
		}
	}

	[Fact]
	public async Task CanHandleTheSameTxSentManyTimesAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync();
		using HostedServices services = new();
		services.Register<MempoolMirror>(() => new MempoolMirror(TimeSpan.FromSeconds(2), coreNode.RpcClient, coreNode.P2pNode), "Mempool Mirror");
		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;
			await services.StartAllAsync();
			var mempoolInstance = services.Get<MempoolMirror>();

			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			using var k1 = new Key();
			var blockIds = await rpc.GenerateToAddressAsync(1, k1.PubKey.WitHash.GetAddress(network));
			var block = await rpc.GetBlockAsync(blockIds[0]);
			var coinBaseTx = block.Transactions[0];

			var tx = Transaction.Create(network);
			tx.Inputs.Add(coinBaseTx, 0);
			tx.Outputs.Add(Money.Coins(49.9999m), BitcoinFactory.CreateBitcoinAddress(network));
			tx.Sign(k1.GetBitcoinSecret(network), coinBaseTx.Outputs.AsCoins().First());
			var valid = tx.Check();

			await rpc.GenerateAsync(101);

			for (int i = 0; i < 5; i++)
			{
				await rpc.SendRawTransactionAsync(tx);
			}

			while ((await rpc.GetRawMempoolAsync()).Length == 0)
			{
				await Task.Delay(50);
			}

			await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

			var localMempoolHashes = mempoolInstance.GetMempoolHashes();

			Assert.Single(localMempoolHashes);
			Assert.Contains(tx.GetHash(), localMempoolHashes);
		}
		finally
		{
			await services.StopAllAsync();
			await coreNode.TryStopAsync(true, 2);
		}
	}

	[Fact]
	public async Task CanHandleManyTxsAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync();
		using HostedServices services = new();
		services.Register<MempoolMirror>(() => new MempoolMirror(TimeSpan.FromSeconds(2), coreNode.RpcClient, coreNode.P2pNode), "Mempool Mirror");
		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;
			await services.StartAllAsync();
			var mempoolInstance = services.Get<MempoolMirror>();

			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			using var k1 = new Key();
			var blockIds = await rpc.GenerateToAddressAsync(1, k1.PubKey.WitHash.GetAddress(network));
			var block = await rpc.GetBlockAsync(blockIds[0]);
			var coinBaseTx = block.Transactions[0];

			await rpc.GenerateAsync(101);

			for (int i = 0; i < 5; i++)
			{
				await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), new Money(0.0004m, MoneyUnit.BTC));
			}

			while ((await rpc.GetRawMempoolAsync()).Length != 5)
			{
				await Task.Delay(50);
			}

			await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

			var localMempoolHashes = mempoolInstance.GetMempoolHashes();

			Assert.Equal(5, localMempoolHashes.Count);
		}
		finally
		{
			await services.StopAllAsync();
			await coreNode.TryStopAsync(true, 2);
		}
	}

	[Fact]
	public async Task CanHandleConfirmationAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync();
		using HostedServices services = new();
		services.Register<MempoolMirror>(() => new MempoolMirror(TimeSpan.FromSeconds(2), coreNode.RpcClient, coreNode.P2pNode), "Mempool Mirror");
		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;
			await services.StartAllAsync();
			var mempoolInstance = services.Get<MempoolMirror>();

			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			var spendAmount = new Money(0.0004m, MoneyUnit.BTC);

			await rpc.GenerateAsync(101);

			var rpcMempoolBeforeSend = await rpc.GetRawMempoolAsync();
			var localMempoolBeforeSend = mempoolInstance.GetMempoolHashes();
			Assert.Equal(rpcMempoolBeforeSend.Length, localMempoolBeforeSend.Count);

			await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), spendAmount);
			while ((await rpc.GetRawMempoolAsync()).Length == 0)
			{
				await Task.Delay(50);
			}

			await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

			var localMempoolAfterSend = mempoolInstance.GetMempoolHashes();
			Assert.Single(localMempoolAfterSend);

			await rpc.GenerateAsync(1);
			while ((await rpc.GetRawMempoolAsync()).Length != 0)
			{
				await Task.Delay(50);
			}
			await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

			var localMempoolAfterBlockMined = mempoolInstance.GetMempoolHashes();
			Assert.Empty(localMempoolAfterBlockMined);
		}
		finally
		{
			await services.StopAllAsync();
			await coreNode.TryStopAsync(true, 2);
		}
	}
}
