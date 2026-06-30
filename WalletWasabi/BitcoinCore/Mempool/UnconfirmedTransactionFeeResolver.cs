using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.BitcoinCore.Mempool;

public static class UnconfirmedTransactionFeeResolver
{
	public static async Task<Money> ComputeFeeAsync(
		Transaction transaction,
		IEnumerable<Transaction> mempoolParentTransactions,
		Func<OutPoint, CancellationToken, Task<TxOut?>> getConfirmedTxOutAsync,
		CancellationToken cancellationToken)
	{
		var mempoolParentsById = mempoolParentTransactions.ToDictionary(x => x.GetHash(), x => x);
		var inputs = new List<Coin>();

		foreach (var input in transaction.Inputs)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var prevOut = input.PrevOut;
			TxOut? txOut;
			if (mempoolParentsById.TryGetValue(prevOut.Hash, out var parentTx))
			{
				txOut = parentTx.Outputs[checked((int)prevOut.N)];
			}
			else
			{
				txOut = await getConfirmedTxOutAsync(prevOut, cancellationToken).ConfigureAwait(false);
			}

			if (txOut is null)
			{
				throw new InvalidOperationException($"Failed to resolve prevout '{prevOut}' from Bitcoin Core.");
			}

			inputs.Add(new Coin(prevOut, txOut));
		}

		return transaction.GetFee(inputs.ToArray());
	}
}
