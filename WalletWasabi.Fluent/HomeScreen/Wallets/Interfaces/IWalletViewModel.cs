using NBitcoin;

namespace WalletWasabi.Fluent.HomeScreen.Wallets.Interfaces;

public interface IWalletViewModel
{
	void SelectTransaction(uint256 txid);
}
