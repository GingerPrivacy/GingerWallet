using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.HomeScreen.Send.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public class WalletCoinsModel(Wallet wallet, WalletModel walletModel) : CoinListModel(wallet, walletModel)
{
	public async Task UpdateExcludedCoinsFromCoinjoinAsync(CoinModel[] coinsToExclude)
	{
		await Task.Run(() =>
		{
			var outPoints = coinsToExclude.Select(x => x.GetSmartCoin().Outpoint).ToArray();
			Wallet.UpdateExcludedCoinsFromCoinJoin(outPoints);
		});
	}

	public List<CoinModel> GetSpentCoins(BuildTransactionResult? transaction)
	{
		var coins = (transaction?.SpentCoins ?? new List<SmartCoin>()).ToList();
		return coins.Select(GetCoinModel).ToList();
	}

	public bool AreEnoughToCreateTransaction(TransactionInfo transactionInfo, IEnumerable<CoinModel> coins)
	{
		return TransactionHelpers.TryBuildTransactionWithoutPrevTx(Wallet.KeyManager, transactionInfo, Wallet.Coins, coins.GetSmartCoins(), Wallet.Kitchen.SaltSoup(), out _);
	}

	protected override Pocket[] GetPockets()
	{
		return Wallet.GetPockets().ToArray();
	}

	protected override CoinModel[] CreateCoinModels()
	{
		return Wallet.Coins.Select(CreateCoinModel).ToArray();
	}
}
