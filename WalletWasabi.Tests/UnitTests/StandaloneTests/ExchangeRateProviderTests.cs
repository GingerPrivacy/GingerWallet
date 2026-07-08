using GingerCommon.Providers.ExchangeRateProviders;
using System.Linq;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.StandaloneTests;

public class ExchangeRateProviderTests
{
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
}
