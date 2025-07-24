using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.Tiles.ViewModels;

public class WalletBalanceTileViewModel : ActivatableViewModel
{
	public WalletBalanceTileViewModel(WalletModel wallet)
	{
		Wallet = wallet;
		Amounts = wallet.Balances;
	}

	public WalletModel Wallet { get; }

	public IObservable<Amount> Amounts { get; }
}
