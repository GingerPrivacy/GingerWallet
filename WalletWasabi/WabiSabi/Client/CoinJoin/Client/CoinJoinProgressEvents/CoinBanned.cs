using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class CoinBanned : CoinJoinProgressEventArgs
{
	public CoinBanned(SmartCoin coin, DateTimeOffset banUntilUtc, InputBannedReasonEnum[] reasons)
	{
		Coin = coin;
		BanUntilUtc = banUntilUtc;
		Reasons = reasons;
	}

	public SmartCoin Coin { get; }
	public DateTimeOffset BanUntilUtc { get; }
	public InputBannedReasonEnum[] Reasons { get; }
}
