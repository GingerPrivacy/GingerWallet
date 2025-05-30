using System.Threading.Tasks;
using WalletWasabi.WebClients.BlockchainInfo;
using WalletWasabi.WebClients.Coinbase;
using WalletWasabi.WebClients.CoinGecko;
using WalletWasabi.WebClients.Bitstamp;
using WalletWasabi.WebClients.Gemini;
using Xunit;
using WalletWasabi.Interfaces;
using System.Threading;
using WalletWasabi.WebClients.Coingate;
using System.Linq;

namespace WalletWasabi.Tests.IntegrationTests;

public class ExternalApiTests
{
	[Fact]
	public async Task CoinbaseExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync(new CoinbaseExchangeRateProvider());

	[Fact]
	public async Task BlockchainInfoExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync(new BlockchainInfoExchangeRateProvider());

	[Fact]
	public async Task CoinGeckoExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync(new CoinGeckoExchangeRateProvider());

	[Fact]
	public async Task BitstampExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync(new BitstampExchangeRateProvider());

	[Fact]
	public async Task GeminiExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync(new GeminiExchangeRateProvider());

	[Fact]
	public async Task CoingateExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync(new CoingateExchangeRateProvider());

	private async Task AssertProviderAsync(IExchangeRateProvider provider)
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(3));
		var rates = await provider.GetExchangeRateAsync(timeoutCts.Token).ConfigureAwait(false);

		var usdRate = Assert.Single(rates, x => x.Ticker == "USD");
		Assert.NotEqual(0.0m, usdRate.Rate);
		if (rates.Any(r => r.Ticker == "EUR"))
		{
			var eurRate = Assert.Single(rates, x => x.Ticker == "EUR");
			Assert.NotEqual(0.0m, eurRate.Rate);
		}
	}
}
