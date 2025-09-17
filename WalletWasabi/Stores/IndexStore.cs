using Microsoft.Data.Sqlite;
using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Stores;

/// <summary>
/// Manages to store the filters safely.
/// </summary>
public class IndexStore : IIndexStore, IAsyncDisposable
{
	public IndexStore(string workFolderPath, Network network, SmartHeaderChain smartHeaderChain)
	{
		SmartHeaderChain = smartHeaderChain;
		Network = network;

		workFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
		IoHelpers.EnsureDirectoryExists(workFolderPath);

		IndexFilePath = Path.Combine(workFolderPath, "IndexStore.sqlite");

		if (network == Network.RegTest)
		{
			File.Delete(IndexFilePath);
		}

		IndexStorage = CreateBlockFilterSqliteStorage();
	}

	private BlockFilterSqliteStorage CreateBlockFilterSqliteStorage()
	{
		try
		{
			return BlockFilterSqliteStorage.FromFile(dataSource: IndexFilePath, startingFilter: StartingFilters.GetStartingFilter(Network));
		}
		catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 11) // 11 ~ SQLITE_CORRUPT error code
		{
			Logger.LogError($"Failed to open SQLite storage file because it's corrupted. Deleting the storage file '{IndexFilePath}'.");

			File.Delete(IndexFilePath);
			throw;
		}
	}

	public event EventHandler<FilterModel>? Reorged;

	public event EventHandler<IEnumerable<FilterModel>>? NewFilters;

	private string IndexFilePath { get; }

	/// <summary>NBitcoin network.</summary>
	private Network Network { get; }

	private SmartHeaderChain SmartHeaderChain { get; }

	/// <summary>Task completion source that is completed once a <see cref="InitializeAsync(CancellationToken)"/> finishes.</summary>
	/// <remarks><c>true</c> if it finishes successfully, <c>false</c> in all other cases.</remarks>
	public TaskCompletionSource<bool> InitializedTcs { get; } = new();

	/// <summary>Filter disk storage.</summary>
	/// <remarks>Guarded by <see cref="IndexLock"/>.</remarks>
	private BlockFilterSqliteStorage IndexStorage { get; set; }

	/// <summary>Guards <see cref="IndexStorage"/>.</summary>
	private AsyncLock IndexLock { get; } = new();

	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		try
		{
			using (await IndexLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				cancellationToken.ThrowIfCancellationRequested();

				await InitializeFiltersNoLockAsync(cancellationToken).ConfigureAwait(false);

				// Initialization succeeded.
				InitializedTcs.SetResult(true);
			}
		}
		catch (Exception)
		{
			InitializedTcs.SetResult(false);
			throw;
		}
	}

	/// <remarks>Guarded by <see cref="IndexLock"/>.</remarks>
	private Task InitializeFiltersNoLockAsync(CancellationToken cancellationToken)
	{
		try
		{
			int i = 0;

			// Read last N filters. There is no need to read all of them.
			foreach (FilterModel filter in IndexStorage.FetchLast(n: 5000))
			{
				i++;

				if (!TryProcessFilterNoLock(filter, enqueue: false))
				{
					throw new InvalidOperationException("Index file inconsistency detected.");
				}

				cancellationToken.ThrowIfCancellationRequested();
			}

			Logger.LogDebug($"Loaded {i} lines from the mature index file.");
		}
		catch (InvalidOperationException ex)
		{
			// We found a corrupted entry. Clear the corrupted database and stop here.
			Logger.LogError("Filter index got corrupted. Clearing the filter index...");
			Logger.LogDebug(ex);
			IndexStorage.Clear();
			throw;
		}

		return Task.CompletedTask;
	}

	/// <remarks>Requires <see cref="IndexLock"/> lock acquired.</remarks>
	private bool TryProcessFilterNoLock(FilterModel filter, bool enqueue)
	{
		try
		{
			SmartHeaderChain.AppendTip(filter.Header);

			if (enqueue)
			{
				if (!IndexStorage.TryAppend(filter))
				{
					throw new InvalidOperationException("Failed to append filter to the database.");
				}
			}

			return true;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			return false;
		}
	}

	public async Task AddNewFiltersAsync(IEnumerable<FilterModel> filters)
	{
		using (await IndexLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
		{
			using SqliteTransaction sqliteTransaction = IndexStorage.BeginTransaction();

			int processed = 0;

			try
			{
				foreach (FilterModel filter in filters)
				{
					if (!TryProcessFilterNoLock(filter, enqueue: true))
					{
						throw new InvalidOperationException($"Failed to process filter with height {filter.Header.Height}.");
					}

					processed++;
				}
			}
			finally
			{
				sqliteTransaction.Commit();

				if (processed > 0)
				{
					NewFilters?.Invoke(this, filters.Take(processed));
				}
			}
		}
	}

	public async Task<FilterModel[]> FetchBatchAsync(uint fromHeight, int batchSize, CancellationToken cancellationToken)
	{
		using (await IndexLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			return IndexStorage.Fetch(fromHeight: fromHeight, limit: batchSize).ToArray();
		}
	}

	public Task<FilterModel?> TryRemoveLastFilterAsync()
	{
		return TryRemoveLastFilterIfNewerThanAsync(height: null);
	}

	private async Task<FilterModel?> TryRemoveLastFilterIfNewerThanAsync(uint? height)
	{
		FilterModel? filter;

		using (await IndexLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
		{
			if (height is null)
			{
				if (!IndexStorage.TryRemoveLast(out filter))
				{
					return null;
				}
			}
			else
			{
				if (!IndexStorage.TryRemoveLastIfNewerThan(height.Value, out filter))
				{
					return null;
				}
			}

			if (SmartHeaderChain.TipHeight != filter.Header.Height)
			{
				throw new InvalidOperationException($"{nameof(SmartHeaderChain)} and {nameof(IndexStorage)} are not in sync.");
			}

			SmartHeaderChain.RemoveTip();
		}

		Reorged?.Invoke(this, filter);

		return filter;
	}

	public async Task RemoveAllNewerThanAsync(uint height)
	{
		while (true)
		{
			FilterModel? filterModel = await TryRemoveLastFilterIfNewerThanAsync(height).ConfigureAwait(false);

			if (filterModel is null)
			{
				break;
			}
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		IndexStorage.Dispose();
		return ValueTask.CompletedTask;
	}
}
