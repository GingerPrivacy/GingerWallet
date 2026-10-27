using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;

namespace WalletWasabi.Blockchain.TransactionProcessing;

/// <summary>Retrieves missing previous transactions outside the wallet processing lock.</summary>
public sealed class DeferredTransactionProcessor : IAsyncDisposable
{
	private const int MaxPendingTransactions = 100;
	private const int MaxPreviousTransactions = 2500;
	private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);
	private readonly TransactionProcessor _processor;
	private readonly Network _network;
	private readonly Func<IReadOnlyList<uint256>, CancellationToken, Task<IEnumerable<Transaction>>> _fetch;
	private readonly CancellationTokenSource _stopping = new();
	private readonly SemaphoreSlim _requests = new(2);
	private readonly Dictionary<uint256, PendingTransaction> _pending = new();
	private readonly object _lock = new();
	public event EventHandler? TransactionResolved;

	private sealed class PendingTransaction
	{
		public Queue<ProcessedResult> Requests { get; } = new();
		public Task Task { get; set; } = Task.CompletedTask;
	}

	public DeferredTransactionProcessor(TransactionProcessor processor, Network network, Func<IReadOnlyList<uint256>, CancellationToken, Task<IEnumerable<Transaction>>> fetch)
	{
		_processor = processor;
		_network = network;
		_fetch = fetch;
		_processor.PreviousTransactionsRequired += OnPreviousTransactionsRequired;
	}

	public bool HasPending
	{
		get { lock (_lock) { return _pending.Count != 0; } }
	}

	private void OnPreviousTransactionsRequired(object? sender, ProcessedResult result)
	{
		if (result.MissingPreviousTransactions.Count > MaxPreviousTransactions)
		{
			Drop(result);
			return;
		}

		// Keep the witness and metadata that were actually received, even if another
		// copy of this transaction is modified while the request is in flight.
		var tx = result.Transaction;
		var snapshot = new SmartTransaction(Transaction.Parse(tx.Transaction.ToHex(), _network), tx.Height, tx.BlockHash, tx.BlockIndex, tx.Labels, tx.IsReplacement, tx.IsSpeedup, tx.IsCancellation, tx.FirstSeen);
		var deferred = new ProcessedResult(snapshot)
		{
			MissingPreviousTransactions = result.MissingPreviousTransactions.ToArray(),
			ConflictState = result.ConflictState.ToArray(),
			DeferredTransactions = result.DeferredTransactions.ToArray()
		};
		var witnessHash = snapshot.Transaction.GetWitHash();
		bool dropped = false;
		lock (_lock)
		{
			if (_stopping.IsCancellationRequested)
			{
				return;
			}
			if (_pending.TryGetValue(witnessHash, out var existing))
			{
				if (deferred.DeferredTransactions.Count != 0 || existing.Requests.Count < MaxPendingTransactions)
				{
					existing.Requests.Enqueue(deferred);
				}
				return;
			}
			if (_pending.Count >= MaxPendingTransactions)
			{
				dropped = true;
			}
			else
			{
				var pending = new PendingTransaction();
				pending.Requests.Enqueue(deferred);
				_pending.Add(witnessHash, pending);
				pending.Task = Task.Run(() => ResolveAsync(witnessHash, pending));
			}
		}
		if (dropped) { Drop(deferred); }
	}

	private void Drop(ProcessedResult deferred)
	{
		_processor.CompleteDeferred(deferred.Transaction.Transaction.GetWitHash(), false);
		_processor.Process(IndependentTransactions(deferred));
		TransactionResolved?.Invoke(this, EventArgs.Empty);
	}

	private static IReadOnlyList<SmartTransaction> IndependentTransactions(ProcessedResult deferred)
	{
		var rejected = new HashSet<uint256> { deferred.Transaction.GetHash() };
		var independent = new List<SmartTransaction>();
		foreach (var tx in deferred.DeferredTransactions)
		{
			if (tx.Transaction.Inputs.Any(input => rejected.Contains(input.PrevOut.Hash)))
			{
				rejected.Add(tx.GetHash());
			}
			else
			{
				independent.Add(tx);
			}
		}
		return independent;
	}

	private async Task ResolveAsync(uint256 witnessHash, PendingTransaction pending)
	{
		bool accepted = false;
		bool completed = false;
		IReadOnlyList<SmartTransaction> children = Array.Empty<SmartTransaction>();
		ProcessedResult? deferred = null;
		try
		{
			while (!_stopping.IsCancellationRequested)
			{
				lock (_lock)
				{
					if (deferred is null && !pending.Requests.TryDequeue(out deferred))
					{
						children = _processor.CompleteDeferred(witnessHash, accepted);
						_pending.Remove(witnessHash);
						completed = true;
						break;
					}
				}
				try
				{
					await _requests.WaitAsync(_stopping.Token).ConfigureAwait(false);
					IEnumerable<Transaction> parents;
					try
					{
						using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
						timeout.CancelAfter(TimeSpan.FromSeconds(30));
						parents = await _fetch(deferred.MissingPreviousTransactions, timeout.Token).ConfigureAwait(false);
					}
					finally
					{
						_requests.Release();
					}

					var requested = deferred.MissingPreviousTransactions.ToHashSet();
					var verified = new Dictionary<uint256, Transaction>();
					foreach (var parent in parents)
					{
						var copy = Transaction.Parse(parent.ToHex(), _network);
						var hash = copy.GetHash();
						if (requested.Contains(hash))
						{
							verified[hash] = copy;
						}
					}

					if (requested.All(verified.ContainsKey))
					{
						var result = _processor.Retry(deferred, verified);
						accepted |= result.IsNews || _processor.IsProcessedForWallet(deferred.Transaction);
						if (deferred.DeferredTransactions.Count != 0)
						{
							var remaining = deferred.DeferredTransactions;
							if (!accepted)
							{
								remaining = IndependentTransactions(deferred);
							}
							_processor.Process(remaining);
						}
						deferred = null;
						continue;
					}
				}
				catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
				{
					return;
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
				await Task.Delay(RetryDelay, _stopping.Token).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
		{
		}
		finally
		{
			if (!completed)
			{
				lock (_lock)
				{
					if (_pending.TryGetValue(witnessHash, out var current) && current == pending)
					{
						children = _processor.CompleteDeferred(witnessHash, accepted);
						_pending.Remove(witnessHash);
					}
				}
			}
			if (children.Count != 0)
			{
				ProcessChildren(children);
			}
			TransactionResolved?.Invoke(this, EventArgs.Empty);
		}
	}

	private void ProcessChildren(IEnumerable<SmartTransaction> children)
	{
		foreach (var child in children)
		{
			var result = _processor.Process(child);
			if (!result.WaitingForDeferredParent && result.MissingPreviousTransactions.Count == 0)
			{
				var descendants = _processor.CompleteDeferred(child.Transaction.GetWitHash(), result.IsNews || _processor.IsProcessedForWallet(child));
				ProcessChildren(descendants);
			}
		}
	}

	public async ValueTask DisposeAsync()
	{
		_processor.PreviousTransactionsRequired -= OnPreviousTransactionsRequired;
		Task[] tasks;
		lock (_lock)
		{
			_stopping.Cancel();
			tasks = _pending.Values.Select(x => x.Task).ToArray();
		}
		await Task.WhenAll(tasks).ConfigureAwait(false);
		_requests.Dispose();
		_stopping.Dispose();
	}
}
