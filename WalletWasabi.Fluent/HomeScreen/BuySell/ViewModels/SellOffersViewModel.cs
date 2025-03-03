using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.BuySell.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Lang;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.ViewModels;

public class SellOffersViewModel : OffersViewModel
{
	public SellOffersViewModel(UiContext uiContext, IWalletModel wallet, IEnumerable<OfferModel> offers) : base(uiContext, wallet, offers)
	{
	}

	protected override OfferViewModel CreateOfferViewModel(OfferModel model)
	{
		return new SellOfferViewModel(model, AcceptOfferAsync);
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

			var redirectUrl = await _wallet.BuySellModel.CreateSellOrderAsync(
				offer.ProviderCode,
				offer.CurrencyTo,
				offer.AmountFrom,
				offer.CountryCode,
				offer.StateCode,
				address,
				offer.PaymentMethod);

			await OnOpenInBrowserAsync(redirectUrl);

			UiContext.Navigate().To().SellSuccess(_wallet, offer.ProviderName);
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
