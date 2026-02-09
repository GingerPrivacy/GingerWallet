using GingerCommon.Static;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GingerCommon.Providers.ExchangeRateProviders;

public class MempoolSpaceExchangeRateProvider : ExchangeRateProvider
{
	public MempoolSpaceExchangeRateProvider()
	{
		AutoCurrencyRefresh = true;
	}

	public override async Task<Dictionary<string, decimal>> QueryRateAsync(string currency, EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		var contentString = await HttpUtils.HttpGetAsync(ApiUrl, $"api/v1/prices", torEndpoint, null, cancellationToken);
		var apiUrl = torEndpoint is null
			? ApiUrl
			: OnionApiUrl;

		var contentString = await HttpUtils.HttpGetAsync(apiUrl, $"api/v1/prices", torEndpoint, null, cancellationToken);
		var rates = JsonSerializer.Deserialize<Dictionary<string, decimal>>(contentString, JsonUtils.OptionCaseInsensitive);
		return rates ?? new();
	}

	private const string ApiUrl = "https://mempool.space";
	public const string ApiUrl = "https://mempool.space/";
	public const string OnionApiUrl = "http://mempoolhqx4isw62xs7abwphsq7ldayuidyx2v2oethdhhj6mlo2r6ad.onion/";
}
