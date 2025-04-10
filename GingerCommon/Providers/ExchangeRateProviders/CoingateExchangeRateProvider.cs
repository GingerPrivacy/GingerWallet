using GingerCommon.Static;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GingerCommon.Providers.ExchangeRateProviders;

public class CoingateExchangeRateProvider : ExchangeRateProvider
{
	public CoingateExchangeRateProvider()
	{
		IsTorSupported = false;
		AutoCurrencyRefresh = true;
	}

	public override async Task<Dictionary<string, decimal>> QueryRateAsync(string currency, EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		var contentString = await HttpUtils.HttpGetAsync(ApiUrl, $"api/v2/rates/merchant", torEndpoint, null, cancellationToken);
		var rates = JsonSerializer.Deserialize<JsonRates>(contentString, JsonUtils.OptionCaseInsensitive)?.BTC;
		Dictionary<string, decimal> result = rates.SafeConvert(x => (decimal.TryParse(x, out decimal value), value));
		return result;
	}

	private const string ApiUrl = "https://api.coingate.com";

	private class JsonRates
	{
		public Dictionary<string, string> BTC { get; set; } = new();
	}
}
