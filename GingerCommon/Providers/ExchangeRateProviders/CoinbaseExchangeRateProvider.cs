using GingerCommon.Static;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GingerCommon.Providers.ExchangeRateProviders;

public class CoinbaseExchangeRateProvider : ExchangeRateProvider
{
	public CoinbaseExchangeRateProvider()
	{
		IsTorSupported = false;
		AutoCurrencyRefresh = true;
	}

	public override async Task<Dictionary<string, decimal>> QueryRateAsync(string currency, EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		var contentString = await HttpUtils.HttpGetAsync(ApiUrl, $"v2/exchange-rates?currency=BTC", torEndpoint, null, cancellationToken);
		var rates = JsonSerializer.Deserialize<JsonRates>(contentString, JsonUtils.OptionCaseInsensitive)?.Data?.Rates;
		Dictionary<string, decimal> result = rates.SafeConvert(x => (decimal.TryParse(x, out decimal value), value));
		return result;
	}

	private const string ApiUrl = "https://api.coinbase.com";

	private class JsonRates
	{
		public RatesInfo Data { get; set; } = new();

		public class RatesInfo
		{
			public string Currency { get; set; } = "";
			public Dictionary<string, string> Rates { get; set; } = new();
		}
	}
}
