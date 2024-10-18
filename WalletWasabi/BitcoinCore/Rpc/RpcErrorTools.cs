using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Lang;

namespace WalletWasabi.BitcoinCore.Rpc;

public static class RpcErrorTools
{
	public const string SpentError1 = "bad-txns-inputs-missingorspent";
	public const string SpentError2 = "missing-inputs";
	public const string SpentError3 = "txn-mempool-conflict";
	public const string TooLongMempoolChainError = "too-long-mempool-chain";

	public static Dictionary<string, string> ErrorTranslations { get; } = new Dictionary<string, string>
	{
		[TooLongMempoolChainError] = Resources.CoinInUnconfirmedChain,
		[SpentError1] = Resources.CoinAlreadySpent,
		[SpentError2] = Resources.CoinAlreadySpent,
		[SpentError3] = Resources.CoinAlreadySpent,
		["bad-txns-inputs-duplicate"] = Resources.TransactionDuplicatedInputs,
		["bad-txns-nonfinal"] = Resources.TransactionNotFinal,
		["bad-txns-oversize"] = Resources.TransactionTooBig,

		["invalid password"] = Resources.WrongPassphrase,
		["Invalid wallet name"] = Resources.InvalidWalletName,
		["Wallet name is already taken"] = Resources.WalletNameTaken,
	};

	public static bool IsSpentError(string error)
	{
		return new[] { SpentError1, SpentError2, SpentError3 }.Any(x => error.Contains(x, StringComparison.OrdinalIgnoreCase));
	}

	public static bool IsTooLongMempoolChainError(string error)
		=> error.Contains(TooLongMempoolChainError, StringComparison.OrdinalIgnoreCase);
}
