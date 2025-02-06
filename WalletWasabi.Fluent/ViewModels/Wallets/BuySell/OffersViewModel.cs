using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.BuySell;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using DynamicData;
using DynamicData.Binding;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.ViewModels.Wallets.BuySell;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class OffersViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	private readonly ReadOnlyObservableCollection<OfferViewModel> _offers;
	private readonly string _allText = Resources.All;

	[AutoNotify] private string _selectedPaymentMethod;

	public OffersViewModel(UiContext uiContext, IWalletModel wallet, IEnumerable<OfferModel> offers)
	{
		Title = Resources.Offers;
		UiContext = uiContext;
		_wallet = wallet;

		PaymentMethods = new []{_allText}.Concat(offers.Select(x => x.MethodName).Order()).Distinct();

		_selectedPaymentMethod = uiContext.ApplicationSettings.BuySellConfiguration.BuyPaymentMethod ?? "";
		if (string.IsNullOrEmpty(_selectedPaymentMethod) || !PaymentMethods.Contains(_selectedPaymentMethod))
		{
			SelectedPaymentMethod = _allText;
		}

		SetupCancel(enableCancel: true, enableCancelOnEscape: false, enableCancelOnPressed: true);
		EnableBack = true;

		this.WhenAnyValue(x => x.SelectedPaymentMethod)
			.Where(p => !string.IsNullOrEmpty(p))
			.Skip(1)
			.Do(p =>
			{
				var conf = uiContext.ApplicationSettings.BuySellConfiguration;
				uiContext.ApplicationSettings.BuySellConfiguration = conf with { BuyPaymentMethod = p };
			})
			.Subscribe();

		var filter = this.WhenAnyValue(x => x.SelectedPaymentMethod).Where(p => !string.IsNullOrEmpty(p)).Select(PaymentMethodFilter);
		offers
			.Select(x => new OfferViewModel(x, AcceptOfferAsync))
			.ToObservable()
			.ToObservableChangeSet(x => x.Key)
			.Filter(filter)
			.Sort(SortExpressionComparer<OfferViewModel>.Descending(x => x.Amount))
			.Bind(out _offers)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe();
	}

	public ReadOnlyObservableCollection<OfferViewModel> Offers => _offers;

	public IEnumerable<string> PaymentMethods { get; }

	private async Task AcceptOfferAsync(OfferViewModel viewModel)
	{
		var offer = viewModel.Offer;

		try
		{
			IsBusy = true;

			var address = GetAddress(offer.ProviderName);

			if (string.IsNullOrEmpty(address) || !await _wallet.BuyModel.ValidateAddressAsync(address))
			{
				var errorMessage = _wallet.Network != Network.Main
					? "You are not on MainNet, please specify a correct MainNet testing address in UiConfig."
					: Resources.InvalidBTCAddress;

				await ShowErrorAsync(Resources.Offers, errorMessage, "");
				return;
			}

			var order = await _wallet.BuyModel.CreateOrderAsync(
				offer.ProviderCode,
				offer.CurrencyFrom,
				offer.AmountFrom.ToString(CultureInfo.InvariantCulture),
				offer.CountryCode,
				offer.StateCode,
				address,
				offer.PaymentMethod);

			await OnOpenInBrowserAsync(order.RedirectUrl);

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

	private string GetAddress(string label)
	{
		if (UiContext.ApplicationSettings.Network != Network.Main)
		{
			return UiContext.ApplicationSettings.BuySellConfiguration.TestingAddress ?? "";
		}

		var address = _wallet.Addresses.NextReceiveAddress([label]);
		address.Hide();

		return address.Text;
	}

	private async Task OnOpenInBrowserAsync(string url)
	{
		try
		{
			await WebBrowserService.Instance.OpenUrlInPreferredBrowserAsync(url).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogError($"Failed to open browser!", ex);
			await ShowErrorAsync(Resources.Browser, ex.ToUserFriendlyString(), Resources.BrowserError);
		}
	}

	private Func<OfferViewModel, bool> PaymentMethodFilter(string method)
	{
		return item =>
		{
			if (method == _allText)
			{
				return true;
			}

			return item.MethodName == method;
		};
	}
}
