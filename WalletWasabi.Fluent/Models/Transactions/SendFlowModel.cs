using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.HomeScreen.Send.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Transactions;

public record SendFlowModel
{
	private SendFlowModel(Wallet wallet, ICoinsView availableCoins, CoinListModel coinListModel, WalletModel walletModel)
	{
		Wallet = wallet;
		AvailableCoins = availableCoins;
		CoinList = coinListModel;
		WalletModel = walletModel;
	}

	/// <summary>Regular Send Flow. Uses all wallet coins</summary>
	public SendFlowModel(Wallet wallet, WalletModel walletModel) :
		this(wallet, wallet.Coins, walletModel.Coins, walletModel)
	{
	}

	/// <summary>Manual Control Send Flow. Uses only the specified coins.</summary>
	public SendFlowModel(Wallet wallet, WalletModel walletModel, IEnumerable<SmartCoin> coins) :
		this(wallet, new CoinsView(coins), new UserSelectionCoinListModel(wallet, walletModel, coins.ToArray()), walletModel)
	{
	}

	public Wallet Wallet { get; }

	public WalletModel WalletModel { get; }

	public ICoinsView AvailableCoins { get; }

	public CoinListModel CoinList { get; }

	public TransactionInfo? TransactionInfo { get; init; } = null;

	public decimal AvailableAmountBtc => AvailableAmount.ToDecimal(MoneyUnit.BTC);

	public Money AvailableAmount => AvailableCoins.TotalAmount();

	public bool IsManual => AvailableCoins.TotalAmount() != Wallet.Coins.TotalAmount();

	public Pocket[] GetPockets() =>
		AvailableCoins.GetPockets(Wallet.AnonScoreTarget)
			.Select(x => new Pocket(x))
			.ToArray();
}
