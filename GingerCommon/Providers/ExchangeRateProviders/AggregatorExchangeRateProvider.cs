using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System;
using System.Collections.Immutable;

namespace GingerCommon.Providers.ExchangeRateProviders;

public class AggregatorExchangeRateProvider : ExchangeRateProvider
{
	public AggregatorExchangeRateProvider()
	{
		// In precedence order, 47 currencies supported total via Tor
		_providers = [
			new BlockchainInfoExchangeRateProvider(), // 28 currencies
			new MempoolSpaceExchangeRateProvider(), // 7 currencies (USD, EUR, GBP, CAD, CHF, AUD, JPY)
			new CoinbaseExchangeRateProvider(), // no tor, 151 currencies,
			new CoinGeckoExchangeRateProvider(), // 45 currencies, needs User-Agent for the request, single fiat query
			new CoingateExchangeRateProvider(), // no tor, 40 currencies,
			new GeminiExchangeRateProvider(), // 4 currencies (EUR, GBP, SGD, USD), single fiat query
			];

		_providersTor = _providers.Where(x => x.IsTorSupported).ToList();
		_providerCurrencies = [];
	}

	public override async Task<ImmutableSortedSet<string>> GetSupportedCurrenciesAsync(EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		var providers = torEndpoint is null ? _providers : _providersTor;

		Task<ImmutableSortedSet<string>>[] tasks = providers.Select(x => x.GetSupportedCurrenciesAsync(torEndpoint, cancellationToken)).ToArray();
		var providerCurrencies = await Task.WhenAll(tasks);

		if (providerCurrencies is null || (providerCurrencies.Length == _providerCurrencies.Length && providerCurrencies.Zip(_providerCurrencies).All(x => x.First == x.Second)))
		{
			return Currencies;
		}

		SortedSet<string> currencies = new();
		foreach (var item in providerCurrencies)
		{
			if (item is not null)
			{
				currencies.UnionWith(item);
			}
		}

		lock (RefreshLock)
		{
			_providerCurrencies = providerCurrencies;
			if (!Enumerable.SequenceEqual(Currencies, currencies))
			{
				// We change the object itself only if there are real change
				Currencies = currencies.ToImmutableSortedSet();
			}
			CurrenciesLastUpdated = DateTimeOffset.UtcNow;
		}

		return Currencies;
	}

	public override async Task<ExchangeRate?> GetRateAsync(string currency, TimeSpan refreshTime, EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		var providers = torEndpoint is null ? _providers : _providersTor;

		var now = DateTimeOffset.UtcNow;
		ExchangeRate? rate = null;
		foreach (ExchangeRateProvider provider in providers)
		{
			var rateLast = await provider.GetRateAsync(currency, refreshTime, torEndpoint, cancellationToken).ConfigureAwait(false);
			if (rateLast is not null && (rate is null || rate.LastUpdated < rateLast.LastUpdated))
			{
				rate = rateLast;
				if (now - rate.LastUpdated < refreshTime)
				{
					return rate;
				}
			}
		}

		return rate;
	}

	public override Task<List<string>> QuerySupportedCurrenciesAsync(EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		return Task.FromResult<List<string>>([]);
	}

	public override Task<Dictionary<string, decimal>> QueryRateAsync(string currency, EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		return Task.FromResult<Dictionary<string, decimal>>(new());
	}

	private List<ExchangeRateProvider> _providers;
	private List<ExchangeRateProvider> _providersTor;

	private ImmutableSortedSet<string>[] _providerCurrencies;
}
