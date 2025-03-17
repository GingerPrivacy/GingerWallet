using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WebClients.CoinGecko;

public class CoinGeckoExchangeRateProvider : IExchangeRateProvider
{
	public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync(CancellationToken cancellationToken)
	{
		// Only used by the Backend.
#pragma warning disable RS0030 // Do not use banned APIs
		using var httpClient = new HttpClient
		{
			BaseAddress = new Uri("https://api.coingecko.com")
		};
#pragma warning restore RS0030 // Do not use banned APIs

		httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GingerWallet", Constants.ClientVersion.ToString()));

		var exchangeRates = new List<ExchangeRate>();

		var currenciesToFetch = new[]
		{
			"usd",
			"eur",
			"cny",
			"huf",
		};

		foreach (var currency in currenciesToFetch)
		{
			using var response = await httpClient.GetAsync($"api/v3/coins/markets?vs_currency={currency}&ids=bitcoin", cancellationToken).ConfigureAwait(false);
			using var content = response.Content;
			try
			{
				var rates = await content.ReadAsJsonAsync<CoinGeckoExchangeRate[]>().ConfigureAwait(false);

				exchangeRates.Add(new ExchangeRate { Rate = rates[0].Rate, Ticker = currency.ToUpper(CultureInfo.InvariantCulture) });
			}
			catch (JsonSerializationException ex)
			{
				var text = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				if (text.Contains("rate limits"))
				{
					continue;
				}

				throw new JsonSerializationException($"JSON serialization error: '{text}'", ex);
			}
		}

		return exchangeRates;
	}
}
