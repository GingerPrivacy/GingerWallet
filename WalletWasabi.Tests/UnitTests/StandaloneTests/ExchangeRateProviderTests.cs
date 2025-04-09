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
		using CancellationTokenSource cts = new(waitTokenSec * 1000);

		var endPoint = tor ? TorHttpClientFactory.TorEndpoint : null;

		var refreshTime = TimeSpan.FromMinutes(15);

		var resUSD = await provider.GetRateAsync("usd", refreshTime, endPoint, cts.Token);
		Assert.True(resUSD is not null, message);
		var resEUR = await provider.GetRateAsync("EUR", refreshTime, endPoint, cts.Token);
		Assert.True(resEUR is not null, message);
		var curr = await provider.GetSupportedCurrenciesAsync(endPoint, cts.Token);
		Assert.True(curr.Count >= currencyCount, message);
	}

	[Fact]
	private void ValidCurrencyTest()
	{
		// 152 currencies
		string[] expected = ["AED", "AFN", "ALL", "AMD", "ANG", "AOA", "ARS", "AUD", "AWG", "AZN", "BAM", "BBD", "BDT", "BGN", "BHD", "BIF", "BMD", "BND", "BOB", "BRL", "BSD", "BTN", "BWP", "BYN", "BZD", "CAD", "CDF", "CHF", "CLP", "CNY", "COP", "CRC", "CUP", "CVE", "CZK", "DJF", "DKK", "DOP", "DZD", "EGP", "ERN", "ETB", "EUR", "FJD", "FKP", "GBP", "GEL", "GHS", "GIP", "GMD", "GNF", "GTQ", "GYD", "HKD", "HNL", "HTG", "HUF", "IDR", "ILS", "INR", "IQD", "IRR", "ISK", "JMD", "JOD", "JPY", "KES", "KGS", "KHR", "KMF", "KPW", "KRW", "KWD", "KYD", "KZT", "LAK", "LBP", "LKR", "LRD", "LYD", "MAD", "MDL", "MGA", "MKD", "MMK", "MNT", "MOP", "MRU", "MUR", "MVR", "MWK", "MXN", "MYR", "MZN", "NAD", "NGN", "NIO", "NOK", "NPR", "NZD", "OMR", "PAB", "PEN", "PGK", "PHP", "PKR", "PLN", "PYG", "QAR", "RON", "RSD", "RUB", "RWF", "SAR", "SBD", "SCR", "SDG", "SEK", "SGD", "SHP", "SLL", "SOS", "SRD", "SSP", "STN", "SYP", "SZL", "THB", "TJS", "TMT", "TND", "TOP", "TRY", "TTD", "TWD", "TZS", "UAH", "UGX", "USD", "UYU", "UZS", "VES", "VND", "VUV", "WST", "XAF", "XCD", "XOF", "XPF", "YER", "ZAR", "ZMW"];
		Assert.Equal(expected, ExchangeRateProvider.ValidCurrencies);
	}

	[Fact]
	public async Task CoinGeckoExchangeRateProviderTestAsync()
	{
		await TestProviderAsync(new CoinGeckoExchangeRateProvider());
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
		await TestProviderAsync(new AggregatorExchangeRateProvider());
	}
}
