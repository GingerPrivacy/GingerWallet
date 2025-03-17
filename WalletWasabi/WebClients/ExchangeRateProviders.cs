using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.WebClients.BlockchainInfo;
using WalletWasabi.WebClients.Coinbase;
using WalletWasabi.WebClients.Bitstamp;
using WalletWasabi.WebClients.CoinGecko;
using WalletWasabi.WebClients.Gemini;
using System.Linq;
using System.Threading;
using WalletWasabi.WebClients.Coingate;

namespace WalletWasabi.WebClients;

public class ExchangeRateProvider : IExchangeRateProvider
{
	private readonly IExchangeRateProvider[] _exchangeRateProviders =
	{
		new BlockchainInfoExchangeRateProvider(),
		new CoinGeckoExchangeRateProvider(),
		new CoinbaseExchangeRateProvider(),
		new CoingateExchangeRateProvider(),
		new BitstampExchangeRateProvider(),
		new GeminiExchangeRateProvider(),
	};

	public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync(CancellationToken cancellationToken)
	{
		IEnumerable<ExchangeRate> bestSoFar = [];

		foreach (var provider in _exchangeRateProviders)
		{
			try
			{
				var result = await provider.GetExchangeRateAsync(cancellationToken).ConfigureAwait(false);

				// Backward compatibility!
				// Always the USD one has to be the first in the list.
				IOrderedEnumerable<ExchangeRate> ordered = result.OrderBy(x => x.Ticker != "USD");

				// We are interested about 4 currencies.
				if (ordered.Count() < 4)
				{
					bestSoFar = ordered;

					// Try the next one;
					continue;
				}

				return ordered.ToArray();
			}
			catch (Exception ex)
			{
				// Ignore it and try with the next one
				Logger.LogTrace(ex);
			}
		}
		return bestSoFar.ToArray();
	}
}
