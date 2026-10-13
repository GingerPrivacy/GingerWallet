using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.BitcoinCore.Rpc.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore;

public class IndexBuilderServiceTests
{
	[Fact]
	public async Task SegwitTaprootUnsynchronizedBitcoinNodeAsync()
	{
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = 0,
				Blocks = 0,
				InitialBlockDownload = false
			}),
		};
		using var blockNotifier = new BlockNotifier(TimeSpan.MaxValue, rpc);
		var indexer = new IndexBuilderService(IndexType.SegwitTaproot, rpc, blockNotifier, "filters.txt");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(1));
		//// Assert.False(indexer.IsRunning);     // <------------ ERROR: it should have stopped but there is a bug for RegTest
		Assert.Throws<ArgumentOutOfRangeException>(() => indexer.GetLastFilter());  // There are no filters
	}

	[Fact]
	public async Task SegwitTaprootStalledBitcoinNodeAsync()
	{
		var called = 0;
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () =>
			{
				called++;
				return Task.FromResult(new BlockchainInfo
				{
					Headers = 10_000,
					Blocks = 0,
					InitialBlockDownload = true
				});
			}
		};
		using var blockNotifier = new BlockNotifier(TimeSpan.MaxValue, rpc);
		var indexer = new IndexBuilderService(IndexType.SegwitTaproot, rpc, blockNotifier, "filters.txt");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(2));
		Assert.True(indexer.IsRunning);  // It is still working
		Assert.Throws<ArgumentOutOfRangeException>(() => indexer.GetLastFilter());  // There are no filters
		Assert.True(called > 1);
	}

	[Fact]
	public async Task SegwitTaprootSynchronizingBitcoinNodeAsync()
	{
		var called = 0;
		var blockchain = GenerateBlockchain().Take(10).ToArray();
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () =>
			{
				called++;
				return Task.FromResult(new BlockchainInfo
				{
					Headers = (ulong)blockchain.Length,
					Blocks = (ulong)called - 1,
					InitialBlockDownload = true
				});
			},
			OnGetBlockHashAsync = (height) => Task.FromResult(blockchain[height].Hash),
			OnGetVerboseBlockAsync = (hash) => Task.FromResult(blockchain.Single(x => x.Hash == hash))
		};
		using var blockNotifier = new BlockNotifier(TimeSpan.MaxValue, rpc);
		var indexer = new IndexBuilderService(IndexType.SegwitTaproot, rpc, blockNotifier, "filters.txt");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(10));
		Assert.True(indexer.IsRunning);  // It is still working

		var lastFilter = indexer.GetLastFilter();
		Assert.Equal(9, (int)lastFilter.Header.Height);
		Assert.True(called > 1);
	}

	[Fact]
	public void IncludeTaprootScriptInFilters()
	{
		var getBlockRpcRawResponse = File.ReadAllText("./UnitTests/Data/VerboseBlock.json");

		var block = RpcParser.ParseVerboseBlockResponse(getBlockRpcRawResponse);
		var filter = IndexBuilderService.BuildFilterForBlock(block, new[] { RpcPubkeyType.TxWitnessV0Keyhash, RpcPubkeyType.TxWitnessV1Taproot });

		var txOutputs = block.Transactions.SelectMany(x => x.Outputs);
		var prevTxOutputs = block.Transactions.SelectMany(x => x.Inputs.Where(y => y.PrevOutput is { }).Select(y => y.PrevOutput));
		var allOutputs = txOutputs.Concat(prevTxOutputs);

		var indexableOutputs = allOutputs.Where(x => x?.PubkeyType is RpcPubkeyType.TxWitnessV0Keyhash or RpcPubkeyType.TxWitnessV1Taproot);
		var nonIndexableOutputs = allOutputs.Except(indexableOutputs);

		static byte[] ComputeKey(uint256 blockId) => blockId.ToBytes()[0..16];

		Assert.All(indexableOutputs, x => Assert.True(filter.Match(x?.ScriptPubKey.ToCompressedBytes(), ComputeKey(block.Hash))));
		Assert.All(nonIndexableOutputs, x => Assert.False(filter.Match(x?.ScriptPubKey.ToCompressedBytes(), ComputeKey(block.Hash))));
	}

	[Fact]
	public void TaprootOnlyScriptInFilters()
	{
		var getBlockRpcRawResponse = File.ReadAllText("./UnitTests/Data/VerboseBlock.json");

		var block = RpcParser.ParseVerboseBlockResponse(getBlockRpcRawResponse);
		var filter = IndexBuilderService.BuildFilterForBlock(block, new[] { RpcPubkeyType.TxWitnessV1Taproot });

		var txOutputs = block.Transactions.SelectMany(x => x.Outputs);
		var prevTxOutputs = block.Transactions.SelectMany(x => x.Inputs.Where(y => y.PrevOutput is { }).Select(y => y.PrevOutput));
		var allOutputs = txOutputs.Concat(prevTxOutputs);

		var indexableOutputs = allOutputs.Where(x => x?.PubkeyType is RpcPubkeyType.TxWitnessV1Taproot);
		var nonIndexableOutputs = allOutputs.Except(indexableOutputs);

		static byte[] ComputeKey(uint256 blockId) => blockId.ToBytes()[0..16];

		Assert.All(indexableOutputs, x => Assert.True(filter.Match(x?.ScriptPubKey.ToCompressedBytes(), ComputeKey(block.Hash))));
		Assert.All(nonIndexableOutputs, x => Assert.False(filter.Match(x?.ScriptPubKey.ToCompressedBytes(), ComputeKey(block.Hash))));
	}

	[Fact]
	public void ThrowsWhenInputPrevoutIsMissing()
	{
		var blockHash = BlockHashFromHeight(1);
		var outPoint = new OutPoint(BlockHashFromHeight(2), 0);
		var transaction = new VerboseTransactionInfo(
			new TransactionBlockInfo(blockHash, DateTimeOffset.UtcNow, 0),
			BlockHashFromHeight(3),
			[new VerboseInputInfo(outPoint)],
			[]);
		var block = new VerboseBlockInfo(
			uint256.Zero,
			1,
			blockHash,
			DateTimeOffset.UtcNow,
			1,
			[transaction]);

		var ex = Assert.Throws<InvalidOperationException>(
			() => IndexBuilderService.BuildFilterForBlock(block, [RpcPubkeyType.TxWitnessV1Taproot]));

		Assert.Contains("missing prevout data", ex.Message);
		Assert.Contains(outPoint.ToString(), ex.Message);
	}

	[Fact]
	public void UsesAuthoritativePrevoutCacheWhenCoreOmitsPrevout()
	{
		var blockHash = BlockHashFromHeight(1);
		var txId = BlockHashFromHeight(2);
		var script = Script.FromHex("51200102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20");
		var output = new VerboseOutputInfo(Money.Satoshis(10_000), script, "witness_v1_taproot");
		var outPoint = new OutPoint(txId, 0);
		var fundingTx = new VerboseTransactionInfo(
			new TransactionBlockInfo(blockHash, DateTimeOffset.UtcNow, 0),
			txId,
			[new VerboseInputInfo("00")],
			[output]);
		var spendingTx = new VerboseTransactionInfo(
			new TransactionBlockInfo(blockHash, DateTimeOffset.UtcNow, 1),
			BlockHashFromHeight(3),
			[new VerboseInputInfo(outPoint)],
			[]);
		var block = new VerboseBlockInfo(
			uint256.Zero,
			1,
			blockHash,
			DateTimeOffset.UtcNow,
			1,
			[fundingTx, spendingTx]);
		var cache = new Dictionary<OutPoint, VerboseOutputInfo>();

		var filter = IndexBuilderService.BuildFilterForBlock(block, [RpcPubkeyType.TxWitnessV1Taproot], cache, isPrevOutputCacheAuthoritative: true);

		static byte[] ComputeKey(uint256 blockId) => blockId.ToBytes()[0..16];

		Assert.True(filter.Match(script.ToCompressedBytes(), ComputeKey(block.Hash)));
		Assert.DoesNotContain(outPoint, cache.Keys);
	}

	[Fact]
	public async Task SegwitTaprootSynchronizedBitcoinNodeAsync()
	{
		var blockchain = GenerateBlockchain().Take(10).ToArray();
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = (ulong)blockchain.Length - 1,
				Blocks = (ulong)blockchain.Length - 1,
				InitialBlockDownload = false
			}),
			OnGetBlockHashAsync = (height) => Task.FromResult(blockchain[height].Hash),
			OnGetVerboseBlockAsync = (hash) => Task.FromResult(blockchain.Single(x => x.Hash == hash))
		};
		using var blockNotifier = new BlockNotifier(TimeSpan.MaxValue, rpc);
		var indexer = new IndexBuilderService(IndexType.SegwitTaproot, rpc, blockNotifier, "filters.txt");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(5));
		Assert.False(indexer.IsRunning);  // we are done

		var result = indexer.GetFilterLinesExcluding(blockchain[0].Hash, 100, out var found);
		Assert.True(found);
		Assert.Equal(9, result.bestHeight.Value);
		Assert.Equal(9, result.filters.Count());
	}

	[Fact]
	public async Task TaprootUnsynchronizedBitcoinNodeAsync()
	{
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = 0,
				Blocks = 0,
				InitialBlockDownload = false
			}),
		};
		using var blockNotifier = new BlockNotifier(TimeSpan.MaxValue, rpc);
		var indexer = new IndexBuilderService(IndexType.Taproot, rpc, blockNotifier, "filters.txt");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(1));
		//// Assert.False(indexer.IsRunning);     // <------------ ERROR: it should have stopped but there is a bug for RegTest
		Assert.Throws<ArgumentOutOfRangeException>(() => indexer.GetLastFilter());  // There are no filters
	}

	[Fact]
	public async Task TaprootStalledBitcoinNodeAsync()
	{
		var called = 0;
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () =>
			{
				called++;
				return Task.FromResult(new BlockchainInfo
				{
					Headers = 10_000,
					Blocks = 0,
					InitialBlockDownload = true
				});
			}
		};
		using var blockNotifier = new BlockNotifier(TimeSpan.MaxValue, rpc);
		var indexer = new IndexBuilderService(IndexType.Taproot, rpc, blockNotifier, "filters.txt");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(2));
		Assert.True(indexer.IsRunning);  // It is still working
		Assert.Throws<ArgumentOutOfRangeException>(() => indexer.GetLastFilter());  // There are no filters
		Assert.True(called > 1);
	}

	[Fact]
	public async Task TaprootSynchronizingBitcoinNodeAsync()
	{
		var called = 0;
		var blockchain = GenerateBlockchain().Take(10).ToArray();
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () =>
			{
				called++;
				return Task.FromResult(new BlockchainInfo
				{
					Headers = (ulong)blockchain.Length,
					Blocks = (ulong)called - 1,
					InitialBlockDownload = true
				});
			},
			OnGetBlockHashAsync = (height) => Task.FromResult(blockchain[height].Hash),
			OnGetVerboseBlockAsync = (hash) => Task.FromResult(blockchain.Single(x => x.Hash == hash))
		};
		using var blockNotifier = new BlockNotifier(TimeSpan.MaxValue, rpc);
		var indexer = new IndexBuilderService(IndexType.Taproot, rpc, blockNotifier, "filters.txt");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(10));
		Assert.True(indexer.IsRunning);  // It is still working

		var lastFilter = indexer.GetLastFilter();
		Assert.Equal(9, (int)lastFilter.Header.Height);
		Assert.True(called > 1);
	}

	[Fact]
	public async Task TaprootSynchronizedBitcoinNodeAsync()
	{
		var blockchain = GenerateBlockchain().Take(10).ToArray();
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = (ulong)blockchain.Length - 1,
				Blocks = (ulong)blockchain.Length - 1,
				InitialBlockDownload = false
			}),
			OnGetBlockHashAsync = (height) => Task.FromResult(blockchain[height].Hash),
			OnGetVerboseBlockAsync = (hash) => Task.FromResult(blockchain.Single(x => x.Hash == hash))
		};
		using var blockNotifier = new BlockNotifier(TimeSpan.MaxValue, rpc);
		var indexer = new IndexBuilderService(IndexType.Taproot, rpc, blockNotifier, "filters.txt");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(5));
		Assert.False(indexer.IsRunning);  // we are done

		var result = indexer.GetFilterLinesExcluding(blockchain[0].Hash, 100, out var found);
		Assert.True(found);
		Assert.Equal(9, result.bestHeight.Value);
		Assert.Equal(9, result.filters.Count());
	}

	private IEnumerable<VerboseBlockInfo> GenerateBlockchain() =>
		from height in Enumerable.Range(0, int.MaxValue).Select(x => (ulong)x)
		select new VerboseBlockInfo(
			BlockHashFromHeight(height),
			height,
			BlockHashFromHeight(height + 1),
			DateTimeOffset.UtcNow.AddMinutes(height * 10),
			height,
			Enumerable.Empty<VerboseTransactionInfo>());

	private static uint256 BlockHashFromHeight(ulong height)
		=> height == 0 ? uint256.Zero : Hashes.DoubleSHA256(BitConverter.GetBytes(height));
}
