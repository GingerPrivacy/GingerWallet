using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class BlockNotifierTests
{
	private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(1);

	[Fact]
	public async Task GenesisBlockOnlyAsync()
	{
		var chain = new ConcurrentChain(Network.RegTest);
		using var notifier = CreateNotifier(chain);
		int blockCount = 0;
		int reorgCount = 0;
		notifier.OnBlock += (_, _) => blockCount++;
		notifier.OnReorg += (_, _) => reorgCount++;

		await StartAndWaitRoundAsync(notifier);

		// No block notifications nor reorg notifications
		Assert.Equal(0, blockCount);
		Assert.Equal(0, reorgCount);

		Assert.Equal(Network.RegTest.GenesisHash, notifier.BestBlockHash);

		await notifier.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task HitGenesisBlockDuringInitializationAsync()
	{
		var chain = new ConcurrentChain(Network.RegTest);
		foreach (var n in Enumerable.Range(0, 3))
		{
			await AddBlockAsync(chain);
		}
		using var notifier = CreateNotifier(chain);
		int blockCount = 0;
		int reorgCount = 0;
		notifier.OnBlock += (_, _) => blockCount++;
		notifier.OnReorg += (_, _) => reorgCount++;

		await StartAndWaitRoundAsync(notifier);

		// No block notifications nor reorg notifications
		Assert.Equal(0, blockCount);
		Assert.Equal(0, reorgCount);

		Assert.Equal(chain.Tip.HashBlock, notifier.BestBlockHash);

		await notifier.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task NotifyBlocksAsync()
	{
		const int BlockCount = 3;
		var chain = new ConcurrentChain(Network.RegTest);
		using var notifier = CreateNotifier(chain);

		var notifiedBlocks = new List<Block>();
		int reorgCount = 0;
		notifier.OnBlock += (_, block) => notifiedBlocks.Add(block);
		notifier.OnReorg += (_, _) => reorgCount++;

		await StartAndWaitRoundAsync(notifier);

		foreach (var n in Enumerable.Range(0, BlockCount))
		{
			await AddBlockAsync(chain);
		}

		await TriggerAndWaitRoundAsync(notifier);

		// Three blocks notifications
		Assert.Equal(BlockCount, notifiedBlocks.Count);
		for (int i = 0; i < notifiedBlocks.Count; i++)
		{
			Assert.Equal(chain.GetBlock(i + 1).HashBlock, notifiedBlocks[i].GetHash());
		}

		// No reorg notifications
		Assert.Equal(0, reorgCount);
		Assert.Equal(chain.Tip.HashBlock, notifier.BestBlockHash);

		await notifier.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task SimpleReorgAsync()
	{
		var chain = new ConcurrentChain(Network.RegTest);
		using var notifier = CreateNotifier(chain);

		var notifiedBlocks = new List<Block>();
		var reorgedBlocks = new List<uint256>();
		notifier.OnBlock += (_, block) => notifiedBlocks.Add(block);
		notifier.OnReorg += (_, blockHash) => reorgedBlocks.Add(blockHash);

		await StartAndWaitRoundAsync(notifier);

		await AddBlockAsync(chain);
		await TriggerAndWaitRoundAsync(notifier);
		var forkPoint = chain.Tip;
		var blockToBeReorged = await AddBlockAsync(chain);
		await TriggerAndWaitRoundAsync(notifier);

		chain.SetTip(forkPoint);
		await AddBlockAsync(chain, wait: false);
		await AddBlockAsync(chain, wait: false);
		await AddBlockAsync(chain);
		await TriggerAndWaitRoundAsync(notifier);

		Assert.Equal(5, notifiedBlocks.Count);

		var reorgedBlock = Assert.Single(reorgedBlocks);
		Assert.Equal(blockToBeReorged.HashBlock, reorgedBlock);
		Assert.Equal(chain.Tip.HashBlock, notifier.BestBlockHash);

		await notifier.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task LongChainReorgAsync()
	{
		var chain = new ConcurrentChain(Network.RegTest);
		using var notifier = CreateNotifier(chain);

		var notifiedBlocks = new List<Block>();
		var reorgedBlocks = new List<uint256>();
		notifier.OnBlock += (_, block) => notifiedBlocks.Add(block);
		notifier.OnReorg += (_, blockHash) => reorgedBlocks.Add(blockHash);

		await StartAndWaitRoundAsync(notifier);

		await AddBlockAsync(chain);
		await TriggerAndWaitRoundAsync(notifier);

		var forkPoint = chain.Tip;
		var firstReorgedChain = new[]
		{
				await AddBlockAsync(chain, wait: false),
				await AddBlockAsync(chain)
			};
		await TriggerAndWaitRoundAsync(notifier);

		chain.SetTip(forkPoint);
		var secondReorgedChain = new[]
		{
				await AddBlockAsync(chain, wait: false),
				await AddBlockAsync(chain, wait: false),
				await AddBlockAsync(chain)
			};
		await TriggerAndWaitRoundAsync(notifier);

		chain.SetTip(secondReorgedChain[1]);
		await AddBlockAsync(chain, wait: false);
		await AddBlockAsync(chain, wait: false);
		await AddBlockAsync(chain, wait: false);
		await AddBlockAsync(chain, wait: false);
		await AddBlockAsync(chain);
		await TriggerAndWaitRoundAsync(notifier);

		Assert.Equal(11, notifiedBlocks.Count);

		Assert.Equal(3, reorgedBlocks.Count);
		var expectedReorgedBlocks = firstReorgedChain.ToList().Concat(new[] { secondReorgedChain[2] });
		Assert.Subset(reorgedBlocks.ToHashSet(), expectedReorgedBlocks.Select(x => x.Header.GetHash()).ToHashSet());
		Assert.Equal(chain.Tip.HashBlock, notifier.BestBlockHash);

		await notifier.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task SuperFastNodeValidationAsync()
	{
		var chain = new ConcurrentChain(Network.RegTest);
		using var notifier = CreateNotifier(chain);
		var blockAwaiter = new EventsAwaiter<Block>(
			h => notifier.OnBlock += h,
			h => notifier.OnBlock -= h,
			144);

		await StartAndWaitRoundAsync(notifier);

		await AddBlockAsync(chain);
		await TriggerAndWaitRoundAsync(notifier);

		foreach (var i in Enumerable.Range(0, 200))
		{
			await AddBlockAsync(chain, wait: false);
		}
		await AddBlockAsync(chain, wait: true);
		await TriggerAndWaitRoundAsync(notifier);

		Assert.Equal(chain.Tip.HashBlock, notifier.BestBlockHash);

		var nofifiedBlocks = (await blockAwaiter.WaitAsync(RoundTimeout)).ToArray();

		var tip = chain.Tip;
		var pos = nofifiedBlocks.Length - 1;
		while (tip.HashBlock != nofifiedBlocks[pos].GetHash())
		{
			tip = tip.Previous;
		}

		while (pos >= 0)
		{
			Assert.Equal(tip.HashBlock, nofifiedBlocks[pos].GetHash());
			tip = tip.Previous;
			pos--;
		}

		await notifier.StopAsync(CancellationToken.None);
	}

	private BlockNotifier CreateNotifier(ConcurrentChain chain)
	{
		var rpc = new MockRpcClient();
		rpc.OnGetBestBlockHashAsync = () => Task.FromResult(chain.Tip.HashBlock);
		rpc.OnGetBlockAsync = (blockHash) =>
		{
			var block = rpc.Network.Consensus.ConsensusFactory.CreateBlock();
			block.Header = chain.GetBlock(blockHash).Header;
			return Task.FromResult(block);
		};

		rpc.OnGetBlockHeaderAsync = (blockHash) => Task.FromResult(chain.GetBlock(blockHash).Header);

		var notifier = new BlockNotifier(TimeSpan.FromHours(1), rpc);
		return notifier;
	}

	private static async Task StartAndWaitRoundAsync(BlockNotifier notifier)
	{
		await notifier.StartAsync(CancellationToken.None);
		await TriggerAndWaitRoundAsync(notifier);
	}

	private static async Task TriggerAndWaitRoundAsync(BlockNotifier notifier)
	{
		await notifier.TriggerAndWaitRoundAsync(RoundTimeout);
	}

	private async Task<ChainedBlock> AddBlockAsync(ConcurrentChain chain, bool wait = true)
	{
		BlockHeader header = Network.RegTest.Consensus.ConsensusFactory.CreateBlockHeader();
		header.Nonce = RandomUtils.GetUInt32();
		header.HashPrevBlock = chain.Tip.HashBlock;
		chain.SetTip(header);
		var block = chain.GetBlock(header.GetHash());
		if (wait)
		{
			await Task.Yield();
		}
		return block;
	}
}
