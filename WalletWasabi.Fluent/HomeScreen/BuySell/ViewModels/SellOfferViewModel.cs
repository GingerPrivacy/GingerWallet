using System.Threading.Tasks;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.BuySell.Models;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.ViewModels;

public class SellOfferViewModel : OfferViewModel
{
	public SellOfferViewModel(OfferModel offer, Func<OfferViewModel, Task> acceptOffer) : base(offer, acceptOffer)
	{
		Amount = offer.AmountTo.ToFormattedFiat(offer.CurrencyTo);
		Fee = offer.Fee.ToFormattedFiat(offer.CurrencyTo);
		FeeToolTip = Resources.AfterDeductingFee.SafeInject(Fee, offer.AmountTo.ToFormattedFiat(offer.CurrencyTo));
	}
}
