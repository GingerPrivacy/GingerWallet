using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.BuySell.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public abstract partial class OffersViewModel : RoutableViewModel
{
	protected readonly IWalletModel _wallet;
	private readonly ReadOnlyObservableCollection<OfferViewModel> _offers;
	private readonly string _allText = Resources.All;

	[AutoNotify] private string _selectedPaymentMethod;

	protected OffersViewModel(IWalletModel wallet, IEnumerable<OfferModel> offers)
	{
		Title = Resources.Offers;
		_wallet = wallet;

		PaymentMethods = new []{_allText}.Concat(offers.Select(x => x.MethodName).Order()).Distinct();

		_selectedPaymentMethod = UiContext.ApplicationSettings.BuySellConfiguration.BuyPaymentMethod ?? "";
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
				var conf = UiContext.ApplicationSettings.BuySellConfiguration;
				UiContext.ApplicationSettings.BuySellConfiguration = conf with { BuyPaymentMethod = p };
			})
			.Subscribe();

		var filter = this.WhenAnyValue(x => x.SelectedPaymentMethod).Where(p => !string.IsNullOrEmpty(p)).Select(PaymentMethodFilter);
		offers
			.Select(CreateOfferViewModel)
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

	protected abstract OfferViewModel CreateOfferViewModel(OfferModel model);

	protected abstract Task AcceptOfferAsync(OfferViewModel viewModel);

	protected string GetAddress(string label)
	{
		if (UiContext.ApplicationSettings.Network != Network.Main)
		{
			return UiContext.ApplicationSettings.BuySellConfiguration.TestingAddress ?? "";
		}

		var address = _wallet.Addresses.NextReceiveAddress([label], ScriptPubKeyType.Segwit);
		address.Hide();

		return address.Text;
	}

	protected async Task OnOpenInBrowserAsync(string url)
	{
		try
		{
			await WebBrowserService.Instance.OpenUrlInPreferredBrowserAsync(url).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
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
