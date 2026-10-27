using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Blockchain.Mempool;

public class MempoolService
{
	/// <summary>Denotes whether we are cleaning up the mempool at the moment or not.</summary>
	private int _cleanupInProcess = 0;

	private long _totalReceives = 0;
	private long _duplicatedReceives = 0;

	public event EventHandler<SmartTransaction>? TransactionReceived;

	/// <remarks>Guarded by <see cref="ProcessedLock"/>.</remarks>
	private HashSet<uint256> ProcessedTransactionHashes { get; } = new();
	private Dictionary<uint256, Queue<Transaction>> TransactionsInFlight { get; } = new();

	/// <summary>Guards <see cref="ProcessedTransactionHashes"/>.</summary>
	private object ProcessedLock { get; } = new();

	/// <summary>Transactions that we would reply to INV messages.</summary>
	/// <remarks>Guarded by <see cref="BroadcastStoreLock"/>.</remarks>
	private List<TransactionBroadcastEntry> BroadcastStore { get; } = new();

	/// <summary>Guards <see cref="BroadcastStore"/>.</summary>
	private object BroadcastStoreLock { get; } = new();

	public bool TrustedNodeMode { get; set; }

	public bool TryAddToBroadcastStore(SmartTransaction transaction, string nodeRemoteSocketEndpoint)
	{
		lock (BroadcastStoreLock)
		{
			if (BroadcastStore.Any(x => x.TransactionId == transaction.GetHash()))
			{
				return false;
			}
			else
			{
				var entry = new TransactionBroadcastEntry(transaction, nodeRemoteSocketEndpoint);
				BroadcastStore.Add(entry);
				return true;
			}
		}
	}

	public bool TryGetFromBroadcastStore(uint256 transactionHash, [NotNullWhen(true)] out TransactionBroadcastEntry? entry)
	{
		lock (BroadcastStoreLock)
		{
			entry = BroadcastStore.FirstOrDefault(x => x.TransactionId == transactionHash);
			return entry is not null;
		}
	}

	public LabelsArray TryGetLabel(uint256 txid)
	{
		var label = LabelsArray.Empty;
		if (TryGetFromBroadcastStore(txid, out var entry))
		{
			label = entry.Transaction.Labels;
		}

		return label;
	}

	/// <summary>
	/// Tries to perform mempool cleanup with the help of the backend.
	/// </summary>
	public async Task<bool> TryPerformMempoolCleanupAsync(WasabiHttpClientFactory httpClientFactory)
	{
		// If already cleaning, then no need to run it that often.
		if (Interlocked.CompareExchange(ref _cleanupInProcess, 1, 0) == 1)
		{
			return false;
		}

		// This function is designed to prevent forever growing mempool.
		try
		{
			lock (ProcessedLock)
			{
				if (ProcessedTransactionHashes.Count == 0)
				{
					// There's nothing to cleanup.
					return true;
				}
			}

			Logger.LogInfo("Start cleaning out mempool...");
			{
				var compactness = 10;
				var allMempoolHashes = await httpClientFactory.SharedWasabiClient.GetMempoolHashesAsync(compactness).ConfigureAwait(false);

				int removedTxCount;

				lock (ProcessedLock)
				{
					removedTxCount = ProcessedTransactionHashes.RemoveWhere(x => !allMempoolHashes.Contains(x.ToString()[..compactness]));
				}

				Logger.LogInfo($"{removedTxCount} transactions were removed from mempool.");
			}

			// Display warning if total receives would be reached by duplicated receives.
			// Also reset the benchmarking.
			var totalReceived = Interlocked.Exchange(ref _totalReceives, 0);
			var duplicatedReceived = Interlocked.Exchange(ref _duplicatedReceives, 0);
			if (duplicatedReceived >= totalReceived && totalReceived != 0)
			{
				// Note that the worst case scenario is not duplicatedReceived == totalReceived, but duplicatedReceived == (number of peers) * totalReceived.
				// It's just duplicatedReceived == totalReceived is maximum what we want to tolerate.
				// By turning off Tor, we can notice that the ratio is much better, so this mainly depends on the internet speed.
				Logger.LogWarning($"Too many duplicated mempool transactions are downloaded.\n{nameof(duplicatedReceived)} : {duplicatedReceived}\n{nameof(totalReceived)} : {totalReceived}");
			}

			return true;
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex);
		}
		finally
		{
			Interlocked.Exchange(ref _cleanupInProcess, 0);
		}

		return false;
	}

	public bool IsProcessed(uint256 txid)
	{
		lock (ProcessedLock)
		{
			return ProcessedTransactionHashes.Contains(txid) && !TransactionsInFlight.ContainsKey(txid);
		}
	}

	internal void Forget(uint256 txid)
	{
		lock (ProcessedLock)
		{
			ProcessedTransactionHashes.Remove(txid);
		}
	}

	public void Process(Transaction tx)
	{
		var txid = tx.GetHash();
		var inFlight = new Queue<Transaction>();

		lock (ProcessedLock)
		{
			if (TransactionsInFlight.TryGetValue(txid, out var waiting))
			{
				// Keep distinct witness variants arriving while the first one is being checked.
				if (waiting.Count < 16 && !waiting.Any(x => x.GetWitHash() == tx.GetWitHash()))
				{
					waiting.Enqueue(tx);
				}
				return;
			}
			if (!ProcessedTransactionHashes.Add(txid))
			{
				Interlocked.Increment(ref _duplicatedReceives);
				Interlocked.Increment(ref _totalReceives);
				return;
			}
			TransactionsInFlight.Add(txid, inFlight);
			Interlocked.Increment(ref _totalReceives);
		}

		try
		{
			while (true)
			{
				TransactionReceived?.Invoke(this, new SmartTransaction(tx, Height.Mempool, labels: TryGetLabel(txid)));
				lock (ProcessedLock)
				{
					var waiting = TransactionsInFlight[txid];
					if (ProcessedTransactionHashes.Contains(txid) || !waiting.TryDequeue(out tx!))
					{
						TransactionsInFlight.Remove(txid);
						return;
					}
					ProcessedTransactionHashes.Add(txid);
				}
			}
		}
		finally
		{
			lock (ProcessedLock)
			{
				if (TransactionsInFlight.TryGetValue(txid, out var current) && ReferenceEquals(current, inFlight))
				{
					TransactionsInFlight.Remove(txid);
				}
			}
		}
	}

	public bool TrySpend(SmartCoin coin, SmartTransaction tx)
	{
		var spent = false;
		lock (BroadcastStoreLock)
		{
			foreach (var foundCoin in BroadcastStore
				.SelectMany(x => x.Transaction.WalletInputs)
				.Concat(BroadcastStore.SelectMany(x => x.Transaction.WalletOutputs))
				.Where(x => x == coin))
			{
				foundCoin.SpenderTransaction = tx;
				spent = true;
			}
		}
		return spent;
	}
}
