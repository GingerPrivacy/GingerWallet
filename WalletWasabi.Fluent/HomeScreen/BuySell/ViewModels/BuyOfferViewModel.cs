using System.Threading.Tasks;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.BuySell.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.ViewModels;

public class BuyOfferViewModel : OfferViewModel
{
	public BuyOfferViewModel(OfferModel offer, Func<OfferViewModel, Task> acceptOffer) : base(offer, acceptOffer)
	{
		Amount = $"≈ {new Amount(Offer.AmountTo).FormattedBtcWithUnit}";
		Fee = offer.Fee.ToFormattedFiat(offer.CurrencyFrom);
		FeeToolTip = Resources.TotalCostIncludesFee.SafeInject(offer.AmountFrom.ToFormattedFiat(offer.CurrencyFrom), Fee);
		IsNoKycVisible = offer.ProviderCode == "wert";
	}
}
