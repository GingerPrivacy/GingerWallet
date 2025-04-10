using GingerCommon.Logging;
using GingerCommon.Providers.ExchangeRateProviders;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services;

public class ExchangeRateService : PeriodicRunner
{
	public ExchangeRateService(TimeSpan period, WasabiHttpClientFactory httpClientFactory, string exchangeCurrency) : base(period)
	{
		HttpClientFactory = httpClientFactory;
		Currency = exchangeCurrency;

		_exchangeRateProvider = new AggregatorExchangeRateProvider();
		ExchangeRate = null;
	}

	public WasabiHttpClientFactory HttpClientFactory { get; }

	public ImmutableSortedSet<string> SupportedCurrencies { get; private set; } = DefaultCurrencies;
	public string Currency { get; private set; } = "USD";
	public ExchangeRate? ExchangeRate { get; private set; }

	public event EventHandler<decimal>? ExchangeRateChanged;

	public event EventHandler<ImmutableSortedSet<string>>? SupportedCurrenciesChanged;

	public static readonly ImmutableSortedSet<string> DefaultCurrencies = [
		"ARS","AUD","BRL","CAD","CHF","CLP","CNY","CZK","DKK","EUR","GBP","HKD","HUF","INR","ISK","JPY","KRW","NGN","NZD","PLN","RON","RUB","SEK","SGD","THB","TRY","TWD","USD"
	];

	public bool Active { get; set; } = true;

	private AggregatorExchangeRateProvider _exchangeRateProvider;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		if (Active)
		{
			await RefreshAsync(TimeSpan.FromMinutes(2), cancel).ConfigureAwait(false);
		}
	}

	public async Task RefreshAsync(TimeSpan refreshTime, CancellationToken cancel)
	{
		try
		{
			var exchangeCurrency = Currency;
			var endPoint = HttpClientFactory.IsTorEnabled ? HttpClientFactory.TorEndpoint : null;
			var exchangeRate = await _exchangeRateProvider.GetRateAsync(exchangeCurrency, refreshTime, endPoint, cancel).ConfigureAwait(false);

			bool exchangeRateChanged = exchangeRate?.Symbol != ExchangeRate?.Symbol || exchangeRate?.Value != ExchangeRate?.Value;

			if (exchangeRate is not null && !SupportedCurrencies.Contains(exchangeCurrency))
			{
				// Make sure that the SupportedCurrency is consistent with the ExchangeRate
				SupportedCurrencies = SupportedCurrencies.Add(exchangeCurrency);
			}
			ExchangeRate = exchangeRate;
			if (exchangeRateChanged)
			{
				try
				{
					ExchangeRateChanged?.Invoke(this, exchangeRate?.Value ?? 0);
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex);
				}
			}

			var currencies = await _exchangeRateProvider.GetSupportedCurrenciesAsync(endPoint, cancel).ConfigureAwait(false);
			bool currenciesChanged = currencies != SupportedCurrencies;

			SupportedCurrencies = currencies;
			if (currenciesChanged)
			{
				try
				{
					SupportedCurrenciesChanged.SafeInvoke(this, currencies);
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Log("ExchangeRateProvider", ex, LogLevel.Warning);
		}
	}
}
