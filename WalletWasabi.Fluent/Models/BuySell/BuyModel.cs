using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Daemon.BuySell;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.BuySell;

[AppLifetime]
[AutoInterface]
public partial class BuyModel
{
	private readonly Wallet _wallet;
	private readonly BuyManager _manager;

	private readonly ReadOnlyObservableCollection<GetOrderModel> _list;
	private readonly SourceCache<GetOrderModel, string> _listCache;

	public BuyModel(Wallet wallet)
	{
		_wallet = wallet;
		_manager = Services.HostedServices.Get<BuyManager>();

		_listCache = new(x => x.OrderId);
		_listCache
			.Connect()
			.Bind(out _list)
			.Subscribe();

		HasHeldOrder = Orders
			.ToObservableChangeSet(x => x.OrderId)
			.Filter(model => model.IsHeld)
			.AsObservableCache()
			.CountChanged
			.Select(x => x > 0);

		HasAny = this.WhenAnyValue(x => x.Orders.Count).Select(count => count > 0);

		Observable
			.FromEventPattern<Wallet>(h => _manager.OrdersUpdated += h, h => _manager.OrdersUpdated -= h)
			.Select(x => x.EventArgs)
			.Where(x => x == _wallet)
			.StartWith(_wallet)
			.DoAsync(async _ => await UpdateOrdersAsync())
			.Subscribe();
	}

	public IObservable<bool> HasAny { get; set; }

	public IObservable<bool> HasHeldOrder { get; set; }

	public ReadOnlyObservableCollection<GetOrderModel> Orders => _list;

	private async Task UpdateOrdersAsync()
	{
		try
		{
			var providers = await GetProviderListAsync();
			var orders = _wallet.KeyManager.Attributes.BuySellWalletData.Orders.Select(x => new GetOrderModel(x, providers.First(p => p.Code == x.ProviderCode)));

			_listCache.Clear();
			_listCache.AddOrUpdate(orders);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	public async Task<CountryModel[]> GetCountriesAsync()
	{
		var result = await _manager.GetAvailableCountriesAsync();
		return result.Countries
			.Select(c => new CountryModel(
				c.Name,
				c.Code,
				c.States?.Select(s => new StateModel(s.Name, s.Code)).OrderBy(x => x.Name).ToArray()))
			.OrderBy(x => x.Name)
			.ToArray();
	}

	public async Task<CurrencyModel[]> GetCurrenciesAsync()
	{
		var result = await _manager.GetCurrenciesAsync();

		return result.Select(x => new CurrencyModel(x.Ticker, x.Name, int.Parse(x.Precision, CultureInfo.InvariantCulture))).OrderBy(x => x.Ticker).ToArray();
	}

	public async Task<OfferModel[]> GetOffersAsync(string currencyFrom, decimal amount, string countryCode, string? stateCode)
	{
		var result = await _manager.GetOffersAsync(currencyFrom, amount, countryCode, stateCode);
		var providers = await GetProviderListAsync();

		return result
			.SelectMany(x => x.PaymentMethodOffers.Select(y => new OfferModel(providers.First(p => p.Code == x.ProviderCode), x.AmountFrom, currencyFrom, countryCode, stateCode, y)))
			.ToArray();
	}

	public async Task<CreateOrderModel> CreateOrderAsync(
		string providerCode,
		string currencyFrom,
		string amountFrom,
		string countryCode,
		string? stateCode,
		string walletAddress,
		string paymentMethod)
	{
		var result = await _manager.CreateOrderAsync(
			_wallet,
			providerCode,
			currencyFrom,
			amountFrom,
			countryCode,
			stateCode,
			walletAddress,
			paymentMethod);

		return new CreateOrderModel(result);
	}

	public async Task<bool> ValidateAddressAsync(string address)
	{
		var result = await _manager.ValidateAddressAsync(address);
		return result.Result;
	}

	private async Task<ProviderModel[]> GetProviderListAsync()
	{
		var result = await _manager.GetProviderListAsync();
		return result.Select(x => new ProviderModel(x)).ToArray();
	}

	public async Task<GetOrderModel[]> GetOrdersAsync()
	{
		var providers = await GetProviderListAsync();
		var result = _manager.GetOrders(_wallet);

		return result.Select(x => new GetOrderModel(x, providers.First(p => p.Code == x.ProviderCode))).ToArray();
	}

	public async Task<(decimal, decimal)> GetLimitsAsync(string currency, string countryCode, string? stateCode)
	{
		return await _manager.GetLimitsAsync(currency, countryCode, stateCode);
	}
}
