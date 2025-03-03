using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.Tiles.ViewModels;

public class WalletBalanceTileViewModel : ActivatableViewModel
{
	public WalletBalanceTileViewModel(IObservable<Amount> amounts)
	{
		Amounts = amounts;
	}

	public IObservable<Amount> Amounts { get; }
}
