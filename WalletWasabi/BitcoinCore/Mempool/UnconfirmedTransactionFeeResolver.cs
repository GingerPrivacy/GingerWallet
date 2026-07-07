using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinCore.Mempool;

public static class UnconfirmedTransactionFeeResolver
{
	public static async Task<Money> ComputeFeeAsync(
		Transaction transaction,
		IEnumerable<Transaction> mempoolParentTransactions,
		Func<OutPoint, CancellationToken, Task<TxOut?>> getConfirmedTxOutAsync,
		CancellationToken cancellationToken)
	{
		var transactionId = transaction.GetHash();
		var mempoolParentsById = mempoolParentTransactions.ToDictionary(x => x.GetHash(), x => x);
		var inputs = new List<Coin>();

		foreach (var input in transaction.Inputs)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var prevOut = input.PrevOut;
			TxOut? txOut;
			if (mempoolParentsById.TryGetValue(prevOut.Hash, out var parentTx))
			{
				if (prevOut.N >= parentTx.Outputs.Count)
				{
					Logger.LogWarning($"Unconfirmed parent transaction '{prevOut.Hash}' did not have output '{prevOut.N}' while computing fee for '{transactionId}'.");
					throw new InvalidOperationException($"Unconfirmed parent transaction '{prevOut.Hash}' did not have output '{prevOut.N}'.");
				}

				txOut = parentTx.Outputs[checked((int)prevOut.N)];
			}
			else
			{
				txOut = await getConfirmedTxOutAsync(prevOut, cancellationToken).ConfigureAwait(false);
			}

			if (txOut is null)
			{
				Logger.LogWarning($"Failed to resolve prevout '{prevOut}' for unconfirmed transaction '{transactionId}' from Bitcoin Core.");
				throw new InvalidOperationException($"Failed to resolve prevout '{prevOut}' from Bitcoin Core.");
			}

			inputs.Add(new Coin(prevOut, txOut));
		}

		return transaction.GetFee(inputs.ToArray());
	}
}
