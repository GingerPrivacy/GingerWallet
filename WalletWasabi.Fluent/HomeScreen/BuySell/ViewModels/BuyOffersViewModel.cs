using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.BuySell.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Lang;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class BuyOffersViewModel : OffersViewModel
{
	public BuyOffersViewModel(WalletModel wallet, IEnumerable<OfferModel> offers) : base(wallet, offers)
	{

	}

	protected override OfferViewModel CreateOfferViewModel(OfferModel model)
	{
		return new BuyOfferViewModel(model, AcceptOfferAsync);
	}

	protected override async Task AcceptOfferAsync(OfferViewModel viewModel)
	{
		var offer = viewModel.Offer;

		try
		{
			IsBusy = true;

			var address = GetAddress(offer.ProviderName);

			if (string.IsNullOrEmpty(address) || !await _wallet.BuySellModel.ValidateAddressAsync(address))
			{
				var errorMessage = _wallet.Network != Network.Main
					? "You are not on MainNet, please specify a correct MainNet testing address in UiConfig."
					: Resources.InvalidBTCAddress;

				await ShowErrorAsync(Resources.Offers, errorMessage, "");
				return;
			}

			var redirectUrl = await _wallet.BuySellModel.CreateBuyOrderAsync(
				offer.ProviderCode,
				offer.CurrencyFrom,
				offer.AmountFrom.ToString(CultureInfo.InvariantCulture),
				offer.CountryCode,
				offer.StateCode,
				address,
				offer.PaymentMethod);

			await OnOpenInBrowserAsync(redirectUrl);

			UiContext.Navigate().To().Success();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Resources.Offers, ex.ToUserFriendlyString(), "");
		}
		finally
		{
			IsBusy = false;
		}
	}
}
