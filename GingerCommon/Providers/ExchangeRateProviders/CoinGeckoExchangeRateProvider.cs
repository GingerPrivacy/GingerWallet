using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using GingerCommon.Static;

namespace GingerCommon.Providers.ExchangeRateProviders;

public class CoinGeckoExchangeRateProvider : ExchangeRateProvider
{
	public override async Task<Dictionary<string, decimal>> QueryRateAsync(string currency, EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		var contentString = await HttpUtils.HttpGetAsync(ApiUrl, $"api/v3/coins/markets?vs_currency={currency.ToLowerInvariant()}&ids=bitcoin", torEndpoint, AddProductInfo, cancellationToken);

		var rates = Parse<JsonRateInfo[]>(contentString);
		Dictionary<string, decimal> result = new();
		if (rates is not null && rates.Length > 0)
		{
			result[currency] = rates[0].Rate;
		}
		return result;
	}

	public override async Task<List<string>> QuerySupportedCurrenciesAsync(EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		var contentString = await HttpUtils.HttpGetAsync(ApiUrl, $"api/v3/simple/supported_vs_currencies", torEndpoint, AddProductInfo, cancellationToken);

		var currencies = Parse<List<string>>(contentString);
		return currencies ?? new();
	}

	private const string ApiUrl = "https://api.coingecko.com";

	private static T? Parse<T>(string contentString) where T : class
	{
		try
		{
			var value = JsonSerializer.Deserialize<T>(contentString);
			return value;
		}
		catch (JsonException ex)
		{
			if (!contentString.Contains("rate limits"))
			{
				throw new JsonException($"JSON serialization error: '{contentString}'", ex);
			}
		}
		return null;
	}

	private static void AddProductInfo(HttpClient httpClient)
	{
		// Needed or blocked by CloudFlare
		httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GingerWallet", "2.0.17+"));
	}

	private class JsonRateInfo
	{
		[JsonPropertyName("current_price")]
		public decimal Rate { get; set; }
	}
}
