using GingerCommon.Logging;
using GingerCommon.Static;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GingerCommon.Providers.ExchangeRateProviders;

public class BlockchainInfoExchangeRateProvider : ExchangeRateProvider
{
	// https://www.blockchain.com/explorer/api/exchange_rates_api

	public BlockchainInfoExchangeRateProvider()
	{
		AutoCurrencyRefresh = true;
	}

	public override async Task<Dictionary<string, decimal>> QueryRateAsync(string currency, EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		var contentString = await HttpUtils.HttpGetAsync(ApiUrl, $"ticker", torEndpoint, null, cancellationToken);
		if (!contentString.SafeTrim().StartsWith('{'))
		{
			Logger.LogError($"The site {ApiUrl} gave back '{contentString}' as response.");
			return [];
		}
		var rates = JsonSerializer.Deserialize<Dictionary<string, JsonRateInfo>>(contentString, JsonUtils.OptionCaseInsensitive);
		Dictionary<string, decimal> result = rates?.Select(x => (x.Key, x.Value.Sell)).ToDictionary() ?? new();
		return result;
	}

	private const string ApiUrl = "https://blockchain.info";

	private record JsonRateInfo(decimal Last, decimal Buy, decimal Sell, string Symbol);
}
