using GingerCommon.Providers.ExchangeRateProviders;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.StandaloneTests;

[Collection("Serial unit tests collection")]
public class ExchangeRateProviderTests : IAsyncLifetime
{
	private WasabiHttpClientFactory TorHttpClientFactory { get; }
	private TorProcessManager TorProcessManager { get; }

	public ExchangeRateProviderTests()
	{
		TorProcessManager = new(Common.TorSettings);
		TorHttpClientFactory = new(Common.TorSocks5Endpoint, backendUriGetter: null);
	}

	public async Task InitializeAsync()
	{
		using CancellationTokenSource startTimeoutCts = new(TimeSpan.FromMinutes(2));

		await TorProcessManager.StartAsync(startTimeoutCts.Token);
	}

	public async Task DisposeAsync()
	{
		await TorHttpClientFactory.DisposeAsync();
		await TorProcessManager.DisposeAsync();
	}

	private async Task TestProviderAsync(ExchangeRateProvider provider, bool tor = true, int waitTokenSec = 30, int currencyCount = 4)
	{
		var message = $"{provider.GetType().Name}, Tor: {tor}";
		var endPoint = tor ? TorHttpClientFactory.TorEndpoint : null;

		var refreshTime = TimeSpan.FromMinutes(15);

		// Let's allow some failures
		const int MaxTries = 5;

		ExchangeRate? resUSD = null;
		for (int tryIdx = 0; tryIdx < MaxTries; tryIdx++)
		{
			using CancellationTokenSource cts = new(waitTokenSec * 1000);
			resUSD = await provider.GetRateAsync("usd", refreshTime, endPoint, cts.Token);
			if (resUSD is not null)
			{
				break;
			}
			if (provider.Currencies.Count == 0)
			{
				provider.ResetCurrencyUpdate();
			}
			await Task.Delay(10000);
		}
		Assert.True(resUSD is not null, message);

		ExchangeRate? resEUR = null;
		for (int tryIdx = 0; tryIdx < MaxTries; tryIdx++)
		{
			using CancellationTokenSource cts = new(waitTokenSec * 1000);
			resEUR = await provider.GetRateAsync("EUR", refreshTime, endPoint, cts.Token);
			if (resEUR is not null)
			{
				break;
			}
			await Task.Delay(10000);
		}
		Assert.True(resEUR is not null, message);

		{
			using CancellationTokenSource cts = new(waitTokenSec * 1000);
			var curr = await provider.GetSupportedCurrenciesAsync(endPoint, cts.Token);
			Assert.True(curr.Count >= currencyCount, message);
		}
	}

	[Fact]
	private void ValidCurrencyTest()
	{
		// 150+ currencies
		string[] expected = ["AED", "AFN", "ALL", "AMD", "ANG", "AOA", "ARS", "AUD", "AWG", "AZN", "BAM", "BBD", "BDT", "BGN", "BHD", "BIF", "BMD", "BND", "BOB", "BRL", "BSD", "BTN", "BWP", "BYN", "BZD", "CAD", "CDF", "CHF", "CLP", "CNY", "COP", "CRC", "CUP", "CVE", "CZK", "DJF", "DKK", "DOP", "DZD", "EGP", "ERN", "ETB", "EUR", "FJD", "FKP", "GBP", "GEL", "GHS", "GIP", "GMD", "GNF", "GTQ", "GYD", "HKD", "HNL", "HTG", "HUF", "IDR", "ILS", "INR", "IQD", "IRR", "ISK", "JMD", "JOD", "JPY", "KES", "KGS", "KHR", "KMF", "KPW", "KRW", "KWD", "KYD", "KZT", "LAK", "LBP", "LKR", "LRD", "LYD", "MAD", "MDL", "MGA", "MKD", "MMK", "MNT", "MOP", "MRU", "MUR", "MVR", "MWK", "MXN", "MYR", "MZN", "NAD", "NGN", "NIO", "NOK", "NPR", "NZD", "OMR", "PAB", "PEN", "PGK", "PHP", "PKR", "PLN", "PYG", "QAR", "RON", "RSD", "RUB", "RWF", "SAR", "SBD", "SCR", "SDG", "SEK", "SGD", "SHP", "SLE", "SOS", "SRD", "SSP", "STN", "SYP", "SZL", "THB", "TJS", "TMT", "TND", "TOP", "TRY", "TTD", "TWD", "TZS", "UAH", "UGX", "USD", "UYU", "UZS", "VED", "VES", "VND", "VUV", "WST", "XAF", "XCD", "XOF", "XPF", "YER", "ZAR", "ZMW"];
		Assert.Equal(expected, ExchangeRateProvider.ValidCurrencies);
	}

	[Fact]
	public async Task CoinGeckoExchangeRateProviderTestAsync()
	{
		// CoinGecko not able to support tor reliably for testing
		await TestProviderAsync(new CoinGeckoExchangeRateProvider(), false);
	}

	[Fact]
	public async Task CoinbaseExchangeRateProviderTestAsync()
	{
		// Tor: Just a moment...
		await TestProviderAsync(new CoinbaseExchangeRateProvider());
	}

	[Fact]
	public async Task GeminiExchangeRateProviderTestAsync()
	{
		await TestProviderAsync(new GeminiExchangeRateProvider());
	}

	[Fact]
	public async Task CoinGateExchangeRateProviderTestAsync()
	{
		// Tor: Why have I been blocked?
		await TestProviderAsync(new CoingateExchangeRateProvider());
	}

	[Fact]
	public async Task BlockchainInfoExchangeRateProviderTestAsync()
	{
		var provider = new BlockchainInfoExchangeRateProvider();
		await TestProviderAsync(provider);
		Assert.Equal(ExchangeRateService.DefaultCurrencies, provider.Currencies);
	}

	[Fact]
	public async Task MempoolSpaceExchangeRateProviderTestAsync()
	{
		await TestProviderAsync(new MempoolSpaceExchangeRateProvider());
	}

	[Fact]
	public async Task AggregatorExchangeRateProviderTestAsync()
	{
		await TestProviderAsync(new AggregatorExchangeRateProvider(), false);
	}
}
