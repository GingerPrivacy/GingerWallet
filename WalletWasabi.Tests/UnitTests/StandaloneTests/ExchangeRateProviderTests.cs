using GingerCommon.Providers.ExchangeRateProviders;
using System.Linq;
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
		Assert.True(ExchangeRateProvider.ValidCurrencies.Count >= 150);
		Assert.All(ExchangeRateProvider.ValidCurrencies, currency =>
		{
			Assert.Equal(3, currency.Length);
			Assert.Equal(currency.ToUpperInvariant(), currency);
		});
		Assert.Equal(ExchangeRateProvider.ValidCurrencies.ToArray(), ExchangeRateProvider.ValidCurrencies.OrderBy(x => x).ToArray());
		Assert.Contains("USD", ExchangeRateProvider.ValidCurrencies);
		Assert.Contains("EUR", ExchangeRateProvider.ValidCurrencies);
		Assert.Contains("HUF", ExchangeRateProvider.ValidCurrencies);
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
