using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.BuySell;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.ViewModels.Wallets.BuySell;

public class OfferViewModel : ViewModelBase
{
	public OfferViewModel(OfferModel offer, Func<OfferViewModel, Task> acceptOffer)
	{
		Offer = offer;
		Amount = $"≈ {Money.Coins(Offer.AmountTo).ToBtcWithUnit()}";
		Fee = offer.Fee.ToFormattedFiat(offer.CurrencyFrom);
		FeeToolTip = string.Format(CultureInfo.InvariantCulture, Resources.TotalCostIncludesFee, offer.AmountFrom.ToFormattedFiat(offer.CurrencyFrom), Fee);
		AcceptCommand = ReactiveCommand.CreateFromTask(async () => await acceptOffer(this));
	}

	public ICommand AcceptCommand { get; }
	public OfferModel Offer { get; }
	public string Amount { get; }
	public string Fee { get; }
	public string FeeToolTip { get; }
	public string MethodName => Offer.MethodName;
	public ComposedKey Key => new(Offer);
}
