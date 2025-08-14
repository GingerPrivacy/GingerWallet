using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WebClients.Bitstamp;

public class BitstampExchangeRateProvider : IExchangeRateProvider
{
	public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync(CancellationToken cancellationToken)
	{
		// Only used by the Backend.
#pragma warning disable RS0030 // Do not use banned APIs
		using var httpClient = new HttpClient
		{
			BaseAddress = new Uri("https://www.bitstamp.net")
		};
#pragma warning restore RS0030 // Do not use banned APIs

		var exchangeRates = new List<ExchangeRate>();

		var currenciesToFetch = new[]
		{
			"usd",
			"eur",
		};

		foreach (var currency in currenciesToFetch)
		{
			using var response = await httpClient.GetAsync($"api/v2/ticker/btc{currency}", cancellationToken).ConfigureAwait(false);
			using var content = response.Content;
			var rate = await content.ReadAsJsonAsync<BitstampExchangeRate>().ConfigureAwait(false);

			exchangeRates.Add(new ExchangeRate { Rate = rate.Rate, Ticker = currency.ToUpper(CultureInfo.InvariantCulture) });
		}

		return exchangeRates;
	}
}
