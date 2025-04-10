using GingerCommon.Static;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GingerCommon.Providers.ExchangeRateProviders;

/// <summary>
/// Very limited currency list due the fact that we use the trade pair symbols directly
/// </summary>
public class GeminiExchangeRateProvider : ExchangeRateProvider
{
	public override async Task<Dictionary<string, decimal>> QueryRateAsync(string currency, EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		var contentString = await HttpUtils.HttpGetAsync(ApiUrl, $"/v1/pubticker/btc{currency.ToLowerInvariant()}", torEndpoint, null, cancellationToken);
		var rate = JsonSerializer.Deserialize<JsonRateInfo>(contentString, JsonUtils.OptionCaseInsensitive);
		Dictionary<string, decimal> result = new();
		if (rate is not null && decimal.TryParse(rate.Bid, out decimal value))
		{
			result[currency] = value;
		}
		return result;
	}

	public override async Task<List<string>> QuerySupportedCurrenciesAsync(EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		var contentString = await HttpUtils.HttpGetAsync(ApiUrl, $"v1/symbols", torEndpoint, null, cancellationToken);
		var tradepairs = JsonSerializer.Deserialize<List<string>>(contentString, JsonUtils.OptionCaseInsensitive);
		return tradepairs?.Where(x => x.StartsWith("btc") && x.Length == 6).Select(x => x[3..]).ToList() ?? new();
	}

	private const string ApiUrl = "https://api.gemini.com";

	private class JsonRateInfo
	{
		public string Bid { get; set; } = "";
	}
}
