using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.BuySell;
using WalletWasabi.Daemon.BuySell;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.BuySell.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.Models;

public class BuySellModel : IDisposable
{
	private readonly CompositeDisposable _disposable = new();

	private readonly Wallet _wallet;
	private readonly BuySellManager _buySellManager;

	private readonly ReadOnlyObservableCollection<GetOrderModel> _buyOrders;
	private readonly ReadOnlyObservableCollection<GetOrderModel> _sellOrders;
	private readonly SourceCache<GetOrderModel, string> _listCache;

	public BuySellModel(Wallet wallet)
	{
		_wallet = wallet;
		_buySellManager = Services.HostedServices.Get<BuySellManager>();

		_listCache = new(x => x.OrderId);

		_listCache
			.Connect()
			.Filter(x => x.IsBuyOrder)
			.Bind(out _buyOrders)
			.Subscribe()
			.DisposeWith(_disposable);

		_listCache
			.Connect()
			.Filter(x => x.IsSellOrder)
			.Bind(out _sellOrders)
			.Subscribe()
			.DisposeWith(_disposable);

		HasBuyOrderOnHold = BuyOrders
			.ToObservableChangeSet(x => x.OrderId)
			.Filter(model => model.IsOnHold)
			.AsObservableCache()
			.CountChanged
			.Select(x => x > 0);

		HasSellOrderOnHold = SellOrders
			.ToObservableChangeSet(x => x.OrderId)
			.Filter(model => model.IsOnHold)
			.AsObservableCache()
			.CountChanged
			.Select(x => x > 0);

		HasAnyBuyOrder = this.WhenAnyValue(x => x.BuyOrders.Count).Select(count => count > 0);
		HasAnySellOrder = this.WhenAnyValue(x => x.SellOrders.Count).Select(count => count > 0);

		Observable
			.FromEventPattern<Wallet>(h => _buySellManager.OrdersUpdated += h, h => _buySellManager.OrdersUpdated -= h)
			.Select(x => x.EventArgs)
			.Where(x => x == _wallet)
			.StartWith(_wallet)
			.DoAsync(async _ => await UpdateOrdersAsync())
			.Subscribe()
			.DisposeWith(_disposable);
	}

	public IObservable<bool> HasAnyBuyOrder { get; set; }
	public IObservable<bool> HasAnySellOrder { get; set; }

	public IObservable<bool> HasBuyOrderOnHold { get; set; }
	public IObservable<bool> HasSellOrderOnHold { get; set; }

	public ReadOnlyObservableCollection<GetOrderModel> BuyOrders => _buyOrders;
	public ReadOnlyObservableCollection<GetOrderModel> SellOrders => _sellOrders;

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

	public async Task<CountryModel[]> GetBuyCountriesAsync()
	{
		var result = await _buySellManager.GetAvailableCountriesAsync(BuySellClientModels.SupportedFlow.Buy);
		return result.Countries.ToModels();
	}

	public async Task<CountryModel[]> GetSellCountriesAsync()
	{
		var result = await _buySellManager.GetAvailableCountriesAsync(BuySellClientModels.SupportedFlow.Sell);
		return result.Countries.ToModels();
	}

	public async Task<CurrencyModel[]> GetBuyCurrenciesAsync()
	{
		var result = await _buySellManager.GetCurrenciesAsync(BuySellClientModels.SupportedFlow.Buy);
		return result.ToModels();
	}

	public async Task<CurrencyModel[]> GetSellCurrenciesAsync()
	{
		var result = await _buySellManager.GetCurrenciesAsync(BuySellClientModels.SupportedFlow.Sell);
		return result.ToModels();
	}

	public async Task<OfferModel[]> GetBuyOffersAsync(string currencyFrom, string currencyTo, decimal amount, string countryCode, string? stateCode)
	{
		var result = await _buySellManager.GetBuyOffersAsync(currencyFrom, currencyTo, amount, countryCode, stateCode);
		var providers = await GetProviderListAsync();

		return result
			.SelectMany(x =>
				x.PaymentMethodOffers.Select(y =>
				{
					var provider = providers.First(p => p.Code == x.ProviderCode);

					return new OfferModel(provider, x.AmountFrom, currencyFrom, currencyTo, countryCode, stateCode, y);
				}))
			.ToArray();
	}

	public async Task<OfferModel[]> GetSellOffersAsync(string currencyFrom, string currencyTo, decimal amount, string countryCode, string? stateCode)
	{
		var result = await _buySellManager.GetSellOffersAsync(currencyFrom, currencyTo, amount, countryCode, stateCode);
		var providers = await GetProviderListAsync();

		return result
			.SelectMany(x =>
				x.PaymentMethodOffers.Select(y =>
				{
					var provider = providers.First(p => p.Code == x.ProviderCode);

					return new OfferModel(provider, x.AmountFrom, currencyFrom, currencyTo, countryCode, stateCode, y);
				}))
			.ToArray();
	}

	public async Task<string> CreateBuyOrderAsync(
		string providerCode,
		string currencyFrom,
		string amountFrom,
		string countryCode,
		string? stateCode,
		string walletAddress,
		string paymentMethod)
	{
		var result = await _buySellManager.CreateBuyOrderAsync(
			_wallet,
			providerCode,
			currencyFrom,
			amountFrom,
			countryCode,
			stateCode,
			walletAddress,
			paymentMethod);

		return result.RedirectUrl.ToString();
	}

	public async Task<string> CreateSellOrderAsync(
		string providerCode,
		string currencyTo,
		decimal amountFrom,
		string countryCode,
		string? stateCode,
		string walletAddress,
		string paymentMethod)
	{
		var result = await _buySellManager.CreateSellOrderAsync(
			_wallet,
			providerCode,
			currencyTo,
			amountFrom,
			countryCode,
			stateCode,
			walletAddress,
			paymentMethod);

		return result.RedirectUrl.ToString();
	}

	public async Task<bool> ValidateAddressAsync(string address)
	{
		var result = await _buySellManager.ValidateAddressAsync(address);
		return result.Result;
	}

	private async Task<ProviderModel[]> GetProviderListAsync()
	{
		var result = await _buySellManager.GetProviderListAsync();
		return result.Select(x => new ProviderModel(x)).ToArray();
	}

	public async Task<GetOrderModel[]> GetOrdersAsync()
	{
		var providers = await GetProviderListAsync();
		var result = _buySellManager.GetOrders(_wallet);

		return result.Select(x => new GetOrderModel(x, providers.First(p => p.Code == x.ProviderCode))).ToArray();
	}

	public async Task<(decimal, decimal)> GetBuyLimitsAsync(string currencyFrom, string countryCode, string? stateCode)
	{
		return await _buySellManager.GetBuyLimitsAsync(currencyFrom, countryCode, stateCode);
	}

	public async Task<(decimal, decimal)> GetSellLimitsAsync(string currencyTo, string countryCode, string? stateCode)
	{
		return await _buySellManager.GetSellLimitsAsync(currencyTo, countryCode, stateCode);
	}

	public void Dispose()
	{
		_disposable.Dispose();
		_listCache.Dispose();
	}
}
