using System.Globalization;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.BuySell.Models;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.ViewModels;

public class SellOfferViewModel : OfferViewModel
{
	public SellOfferViewModel(OfferModel offer, Func<OfferViewModel, Task> acceptOffer) : base(offer, acceptOffer)
	{
		Amount = offer.AmountTo.ToFormattedFiat(offer.CurrencyTo);
		Fee = offer.Fee.ToFormattedFiat(offer.CurrencyTo);
		FeeToolTip = string.Format(CultureInfo.InvariantCulture, "After deducting the {0} fee, you will receive {1} — no further charges apply.", Fee, offer.AmountTo.ToFormattedFiat(offer.CurrencyTo));
	}
}
