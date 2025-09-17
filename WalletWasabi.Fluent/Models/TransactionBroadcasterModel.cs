using NBitcoin;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models;

public class TransactionBroadcasterModel
{
	private readonly Network _network;

	public TransactionBroadcasterModel(Network network)
	{
		_network = network;
	}

	public SmartTransaction? Parse(string text)
	{
		if (PSBT.TryParse(text, _network, out var signedPsbt))
		{
			if (!signedPsbt.IsAllFinalized())
			{
				signedPsbt.Finalize();
			}

			return signedPsbt.ExtractSmartTransaction();
		}
		else
		{
			return new SmartTransaction(Transaction.Parse(text, _network), Height.Unknown);
		}
	}

	public Task<SmartTransaction> LoadFromFileAsync(string filePath)
	{
		return TransactionHelpers.ParseTransactionAsync(filePath, _network);
	}

	public TransactionBroadcastInfo GetBroadcastInfo(SmartTransaction transaction)
	{
		var tx = transaction.Transaction;

		Money spendingSum = tx.Inputs
			.Select(x => Services.BitcoinStore.TransactionStore.TryGetTransaction(x.PrevOut.Hash, out var prevTxn) ? prevTxn.Transaction.Outputs[x.PrevOut.N].Value : Money.Zero)
			.Sum();

		Money outputSum = tx.Outputs.Select(x => x.Value).Sum();
		var networkFee = spendingSum - outputSum;

		var inputAmountString = $"{spendingSum.ToFormattedString()} BTC";
		var outputAmountString = $"{outputSum.ToFormattedString()} BTC";
		var feeString = networkFee.ToFeeDisplayUnitFormattedString();

		return new TransactionBroadcastInfo(tx.GetHash().ToString(), tx.Inputs.Count, tx.Outputs.Count, inputAmountString, outputAmountString, feeString);
	}

	public Task SendAsync(SmartTransaction transaction)
	{
		return Services.TransactionBroadcaster.SendTransactionAsync(transaction);
	}
}
