using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.Tiles.ViewModels;

public class BtcPriceTileViewModel : ActivatableViewModel
{
	public BtcPriceTileViewModel(IAmountProvider amountProvider)
	{
		UsdPerBtc = amountProvider.BtcToUsdExchangeRates;
	}

	public IObservable<decimal> UsdPerBtc { get; }
}
