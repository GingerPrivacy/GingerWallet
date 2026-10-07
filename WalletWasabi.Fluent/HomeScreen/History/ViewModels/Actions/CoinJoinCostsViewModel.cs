using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Fluent.HomeScreen.History.ViewModels.Actions;

public class CoinJoinCostsViewModel
{
	public CoinJoinCostsViewModel(CoinJoinCosts costs, AmountProvider amounts)
	{
		MiningFee = amounts.Create(costs.MiningFee);
		CoordinationFee = amounts.Create(costs.CoordinationFee);
		WastedDust = amounts.Create(costs.WastedDust);
		PaymentsTotal = amounts.Create(costs.PaymentsTotal);
	}

	public Amount MiningFee { get; }
	public Amount CoordinationFee { get; }
	public Amount WastedDust { get; }
	public Amount PaymentsTotal { get; }
}
