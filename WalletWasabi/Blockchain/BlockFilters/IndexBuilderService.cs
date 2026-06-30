using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.BitcoinCore.Rpc.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.BlockFilters;

public class IndexBuilderService
{
	private const long NotStarted = 0;
	private const long Running = 1;
	private const long Stopping = 2;
	private const long Stopped = 3;

	/// <summary>
	/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
	/// </summary>
	private long _serviceStatus;

	private long _workerCount;

	public IndexBuilderService(IndexType indexType, IRPCClient rpc, BlockNotifier blockNotifier, string indexFilePath)
	{
		IndexType = indexType;
		RpcClient = Guard.NotNull(nameof(rpc), rpc);
		BlockNotifier = Guard.NotNull(nameof(blockNotifier), blockNotifier);
		IndexFilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(indexFilePath), indexFilePath);

		PubKeyTypes = IndexTypeConverter.ToRpcPubKeyTypes(IndexType);

		StartingHeight = SmartHeader.GetStartingHeader(RpcClient.Network, IndexType).Height;

		_serviceStatus = NotStarted;

		IoHelpers.EnsureContainingDirectoryExists(IndexFilePath);

		// Testing permissions.
		using (var _ = File.Open(IndexFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
		{
		}

		if (File.Exists(IndexFilePath))
		{
			if (RpcClient.Network == Network.RegTest)
			{
				File.Delete(IndexFilePath); // RegTest is not a global ledger, better to delete it.
			}
			else
			{
				List<FilterModel> newList = new(1000_000);
				foreach (var line in File.ReadAllLines(IndexFilePath))
				{
					var filter = FilterModel.FromLine(line);
					newList.Add(filter);
				}

				_indexListLock.EnterWriteLock();
				try
				{
					_blockIndex.Clear();
					for (int idx = 0, len = newList.Count; idx < len; idx++)
					{
						_blockIndex.Add(newList[idx].Header.BlockHash, idx);
					}
					_indexList = newList;
				}
				finally
				{
					_indexListLock.ExitWriteLock();
				}
			}
		}

		IsPrevOutputCacheAuthoritative = _indexList.Count == 0;

		BlockNotifier.OnBlock += BlockNotifier_OnBlock;
	}

	public static byte[][] DummyScript { get; } = [Convert.FromHexString("0009BBE4C2D17185643765C265819BF5261755247D")];

	private IRPCClient RpcClient { get; }
	private BlockNotifier BlockNotifier { get; }
	private string IndexFilePath { get; }

	private ReaderWriterLockSlim _indexListLock = new();
	private List<FilterModel> _indexList = [];
	private Dictionary<uint256, int> _blockIndex = [];
	private Dictionary<OutPoint, VerboseOutputInfo> PrevOutputCache { get; } = [];
	private bool IsPrevOutputCacheAuthoritative { get; set; }

	private uint StartingHeight { get; }
	public bool IsRunning => Interlocked.Read(ref _serviceStatus) == Running;
	private bool IsStopping => Interlocked.Read(ref _serviceStatus) >= Stopping;
	public DateTimeOffset LastFilterBuildTime { get; set; }
	private IndexType IndexType { get; }

	private RpcPubkeyType[] PubKeyTypes { get; }

	public static GolombRiceFilter CreateDummyEmptyFilter(uint256 blockHash)
	{
		return new GolombRiceFilterBuilder()
			.SetKey(blockHash)
			.SetP(20)
			.SetM(1 << 20)
			.AddEntries(DummyScript)
			.Build();
	}

	public void Synchronize()
	{
		Task.Run(async () =>
		{
			try
			{
				if (Interlocked.Read(ref _workerCount) >= 2)
				{
					return;
				}

				if (Interlocked.Increment(ref _workerCount) != 1)
				{
					while (Interlocked.Read(ref _workerCount) != 1)
					{
						await Task.Delay(100).ConfigureAwait(false);
					}
				}

				if (IsStopping)
				{
					return;
				}

				try
				{
					Interlocked.Exchange(ref _serviceStatus, Running);

					while (IsRunning)
					{
						try
						{
							SyncInfo syncInfo = await GetSyncInfoAsync().ConfigureAwait(false);

							FilterModel? lastIndexFilter;

							_indexListLock.EnterReadLock();
							try
							{
								lastIndexFilter = _indexList.Count > 0 ? _indexList[^1] : null;
							}
							finally
							{
								_indexListLock.ExitReadLock();
							}

							uint currentHeight;
							uint256? currentHash;

							if (lastIndexFilter is not null)
							{
								currentHeight = lastIndexFilter.Header.Height;
								currentHash = lastIndexFilter.Header.BlockHash;
							}
							else
							{
								currentHash = StartingHeight == 0
									? uint256.Zero
									: await RpcClient.GetBlockHashAsync((int)StartingHeight - 1).ConfigureAwait(false);
								currentHeight = StartingHeight - 1;
							}

							var coreNotSynced = !syncInfo.IsCoreSynchronized;
							var tipReached = syncInfo.BlockCount == currentHeight;
							var isTimeToRefresh = DateTimeOffset.UtcNow - syncInfo.BlockchainInfoUpdated > TimeSpan.FromMinutes(5);
							if (coreNotSynced || tipReached || isTimeToRefresh)
							{
								syncInfo = await GetSyncInfoAsync().ConfigureAwait(false);
							}

							// If wasabi filter height is the same as core we may be done.
							if (syncInfo.BlockCount == currentHeight)
							{
								// Check that core is fully synced
								if (syncInfo.IsCoreSynchronized && !syncInfo.InitialBlockDownload)
								{
									// Mark the process as not-started, so it can be started again
									// and finally block can mark it as stopped.
									Interlocked.Exchange(ref _serviceStatus, NotStarted);
									return;
								}
								else
								{
									// Knots is catching up give it a 10 seconds
									await Task.Delay(10000).ConfigureAwait(false);
									continue;
								}
							}

							uint nextHeight = currentHeight + 1;
							uint256 blockHash = await RpcClient.GetBlockHashAsync((int)nextHeight).ConfigureAwait(false);
							VerboseBlockInfo block = await RpcClient.GetVerboseBlockAsync(blockHash).ConfigureAwait(false);

							// Check if we are still on the best chain,
							// if not rewind filters till we find the fork.
							if (currentHash != block.PrevBlockHash)
							{
								Logger.LogWarning("Reorg observed on the network.");

								await ReorgOneAsync().ConfigureAwait(false);

								// Skip the current block.
								continue;
							}

							var filter = BuildFilterForBlock(block, PubKeyTypes, PrevOutputCache, IsPrevOutputCacheAuthoritative);

							var smartHeader = new SmartHeader(block.Hash, block.PrevBlockHash, nextHeight, block.BlockTime);
							var filterModel = new FilterModel(smartHeader, filter);

							await File.AppendAllLinesAsync(IndexFilePath, new[] { filterModel.ToLine() }).ConfigureAwait(false);

							_indexListLock.EnterWriteLock();
							try
							{
								_blockIndex.Add(filterModel.Header.BlockHash, _indexList.Count);
								_indexList.Add(filterModel);
							}
							finally
							{
								_indexListLock.ExitWriteLock();
							}

							// If not close to the tip, just log debug.
							if (syncInfo.BlockCount - nextHeight <= 3 || nextHeight % 100 == 0)
							{
								Logger.LogInfo($"Created {Enum.GetName(IndexType)} filter for block: {nextHeight}.");
							}
							else
							{
								Logger.LogDebug($"Created {Enum.GetName(IndexType)} filter for block: {nextHeight}.");
							}
							LastFilterBuildTime = DateTimeOffset.UtcNow;
						}
						catch (Exception ex)
						{
							Logger.LogDebug(ex);

							// Pause the while loop for a while to not flood logs in case of permanent error.
							await Task.Delay(1000).ConfigureAwait(false);
						}
					}
				}
				finally
				{
					Interlocked.CompareExchange(ref _serviceStatus, Stopped, Stopping); // If IsStopping, make it stopped.
					Interlocked.Decrement(ref _workerCount);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError($"Synchronization attempt failed to start: {ex}");
			}
		});
	}

	internal static GolombRiceFilter BuildFilterForBlock(VerboseBlockInfo block, RpcPubkeyType[] pubKeyTypes) =>
		BuildFilterForBlock(block, pubKeyTypes, prevOutputCache: null, isPrevOutputCacheAuthoritative: false);

	internal static GolombRiceFilter BuildFilterForBlock(
		VerboseBlockInfo block,
		RpcPubkeyType[] pubKeyTypes,
		IDictionary<OutPoint, VerboseOutputInfo>? prevOutputCache,
		bool isPrevOutputCacheAuthoritative)
	{
		var scripts = FetchScripts(block, pubKeyTypes, prevOutputCache, isPrevOutputCacheAuthoritative);

		if (scripts.Count != 0)
		{
			return new GolombRiceFilterBuilder()
				.SetKey(block.Hash)
				.SetP(20)
				.SetM(1 << 20)
				.AddEntries(scripts.Select(x => x.ToCompressedBytes()))
				.Build();
		}
		else
		{
			// We cannot have empty filters, because there was a bug in GolombRiceFilterBuilder that evaluates empty filters to true.
			// And this must be fixed in a backwards compatible way, so we create a fake filter with a random scp instead.
			return CreateDummyEmptyFilter(block.Hash);
		}
	}

	private static List<Script> FetchScripts(
		VerboseBlockInfo block,
		RpcPubkeyType[] pubKeyTypes,
		IDictionary<OutPoint, VerboseOutputInfo>? prevOutputCache,
		bool isPrevOutputCacheAuthoritative)
	{
		var scripts = new List<Script>();

		foreach (var tx in block.Transactions)
		{
			foreach (var input in tx.Inputs)
			{
				if (input.IsCoinbase)
				{
					continue;
				}

				var prevOut = input.PrevOutput;
				if (prevOut is null)
				{
					var outPoint = input.OutPoint;
					if (outPoint is null)
					{
						throw new InvalidOperationException($"Cannot build filter for block '{block.Hash}' because transaction '{tx.Id}' is missing input outpoint data.");
					}

					if (prevOutputCache?.TryGetValue(outPoint, out prevOut) is not true)
					{
						if (isPrevOutputCacheAuthoritative)
						{
							continue;
						}

						throw new InvalidOperationException($"Cannot build filter for block '{block.Hash}' because transaction '{tx.Id}' is missing prevout data for input '{outPoint}', and the local prevout cache was not built from the index starting height.");
					}
				}

				if (pubKeyTypes.Contains(prevOut.PubkeyType))
				{
					scripts.Add(prevOut.ScriptPubKey);
				}

				if (input.OutPoint is { } spentOutPoint)
				{
					prevOutputCache?.Remove(spentOutPoint);
				}
			}

			foreach (var (output, index) in tx.Outputs.Select((output, index) => (output, index)))
			{
				if (pubKeyTypes.Contains(output.PubkeyType))
				{
					scripts.Add(output.ScriptPubKey);
					prevOutputCache?.Add(new OutPoint(tx.Id, (uint)index), output);
				}
			}
		}

		return scripts;
	}

	private async Task ReorgOneAsync()
	{
		// 1. Rollback index.
		uint256 blockHash;

		_indexListLock.EnterWriteLock();
		try
		{
			blockHash = _indexList[^1].Header.BlockHash;
			_blockIndex.Remove(blockHash);
			_indexList.RemoveAt(_indexList.Count - 1);
		}
		finally
		{
			_indexListLock.ExitWriteLock();
		}

		Logger.LogInfo($"REORG invalid block: {blockHash}");

		// 2. Serialize Index. (Remove last line.)
		var lines = await File.ReadAllLinesAsync(IndexFilePath).ConfigureAwait(false);
		await File.WriteAllLinesAsync(IndexFilePath, lines.Take(lines.Length - 1).ToArray()).ConfigureAwait(false);
	}

	private async Task<SyncInfo> GetSyncInfoAsync()
	{
		var bcinfo = await RpcClient.GetBlockchainInfoAsync().ConfigureAwait(false);
		var pbcinfo = new SyncInfo(bcinfo);
		return pbcinfo;
	}

	private void BlockNotifier_OnBlock(object? sender, Block e)
	{
		try
		{
			// Run sync every time a block notification arrives. Synchronizer will stop when it finishes.
			Synchronize();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	public (Height bestHeight, IEnumerable<FilterModel> filters) GetFilterLinesExcluding(uint256 bestKnownBlockHash, int count, out bool found)
	{
		Height bestHeight;
		List<FilterModel>? filters = null;
		{
			_indexListLock.EnterReadLock();
			try
			{
				bestHeight = _indexList.Count > 0 ? (int)_indexList[^1].Header.Height : Height.Unknown;
				if (found = _blockIndex.TryGetValue(bestKnownBlockHash, out int idx))
				{
					int bgn = Math.Min(idx + 1, _indexList.Count);
					int len = Math.Min(count, _indexList.Count - bgn);
					if (len > 0)
					{
						filters = _indexList.GetRange(bgn, len);
					}
				}
			}
			finally
			{
				_indexListLock.ExitReadLock();
			}
		}
		return (bestHeight, filters is not null ? filters : Enumerable.Empty<FilterModel>());
	}

	public FilterModel GetLastFilter()
	{
		_indexListLock.EnterReadLock();
		try
		{
			return _indexList[^1];
		}
		finally
		{
			_indexListLock.ExitReadLock();
		}
	}

	public async Task StopAsync()
	{
		if (BlockNotifier is { })
		{
			BlockNotifier.OnBlock -= BlockNotifier_OnBlock;
		}

		Interlocked.CompareExchange(ref _serviceStatus, Stopping, Running); // If running, make it stopping.

		while (Interlocked.CompareExchange(ref _serviceStatus, Stopped, NotStarted) == 2)
		{
			await Task.Delay(50).ConfigureAwait(false);
		}
	}
}
