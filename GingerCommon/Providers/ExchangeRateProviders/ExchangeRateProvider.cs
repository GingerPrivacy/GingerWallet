using GingerCommon.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace GingerCommon.Providers.ExchangeRateProviders;

public record ExchangeRate(string Symbol, decimal Value, DateTimeOffset LastUpdated);

public abstract class ExchangeRateProvider
{
	// There can be some extra, already replaced currencies in the CultureInfo data that we need to remove manually.
	private static readonly SortedSet<string> ExcludeCurrencies = ["HRK"];

	// Same, but missing ones
	private static readonly SortedSet<string> MissingCurrencies = ["MVR"];

	public static readonly ImmutableSortedSet<string> ValidCurrencies =
		CultureInfo.GetCultures(CultureTypes.SpecificCultures).Select(culture => new RegionInfo(culture.Name).ISOCurrencySymbol).Where(x => x?.Length == 3 && !ExcludeCurrencies.Contains(x)).Concat(MissingCurrencies).ToImmutableSortedSet();

	// If the IsTorSupported bool is false, the abstract query functions always will get null as torEndpoint
	public bool IsTorSupported { get; protected set; } = true;

	// Asking for the rate refreshes the currency list as well
	public bool AutoCurrencyRefresh { get; protected set; } = false;

	protected bool CurrenciesAreUptodate()
	{
		var elapsed = DateTimeOffset.UtcNow - CurrenciesLastUpdated;
		return elapsed.TotalMinutes < 15 || (elapsed.TotalHours < 24 && !Currencies.IsEmpty);
	}

	public virtual async Task<ImmutableSortedSet<string>> GetSupportedCurrenciesAsync(EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		try
		{
			if (CurrenciesAreUptodate())
			{
				return Currencies;
			}

			var currencies = await QuerySupportedCurrenciesAsync(IsTorSupported ? torEndpoint : null, cancellationToken).ConfigureAwait(false);
			UpdateCurrencies(currencies);
		}
		catch (Exception ex)
		{
			Logger.Log($"Exception from {GetType().Name}", ex, LogLevel.Warning);
		}

		return Currencies;
	}

	protected bool UpdateCurrencies(IEnumerable<string>? newCurrencies)
	{
		newCurrencies ??= [];
		ImmutableSortedSet<string> finalCurrencies = ImmutableSortedSet.CreateRange(newCurrencies.Select(x => x.ToUpperInvariant()).Where(ValidCurrencies.Contains));
		lock (RefreshLock)
		{
			CurrenciesLastUpdated = DateTimeOffset.UtcNow;
			if (finalCurrencies.Count > 0 && !Enumerable.SequenceEqual(Currencies, finalCurrencies))
			{
				Currencies = finalCurrencies;
				return true;
			}
		}
		return false;
	}

	public virtual async Task<ExchangeRate?> GetRateAsync(string currency, TimeSpan refreshTime, EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		ExchangeRate? rate = null;
		try
		{
			currency = currency.ToUpperInvariant();
			if (!AutoCurrencyRefresh)
			{
				var currencies = await GetSupportedCurrenciesAsync(torEndpoint, cancellationToken).ConfigureAwait(false);
				if (!currencies.Contains(currency))
				{
					return null;
				}
			}
			else
			{
				if (CurrenciesAreUptodate() && !Currencies.Contains(currency))
				{
					return null;
				}
			}

			lock (RefreshLock)
			{
				_rates.TryGetValue(currency, out rate);
			}
			if (rate is not null && (DateTimeOffset.UtcNow - rate.LastUpdated) < refreshTime)
			{
				return rate;
			}
			var newRates = await QueryRateAsync(currency, IsTorSupported ? torEndpoint : null, cancellationToken).ConfigureAwait(false);
			if (AutoCurrencyRefresh)
			{
				UpdateCurrencies(newRates?.Keys);
			}

			lock (RefreshLock)
			{
				var now = DateTimeOffset.UtcNow;
				foreach (var newRate in newRates ?? [])
				{
					var key = newRate.Key.ToUpperInvariant();
					if (Currencies.Contains(key))
					{
						_rates[key] = new ExchangeRate(key, newRate.Value, now);
					}
				}
				_rates.TryGetValue(currency, out rate);
			}
		}
		catch (Exception ex)
		{
			Logger.Log($"Exception from {GetType().Name}", ex, LogLevel.Warning);
		}

		return rate;
	}

	private Dictionary<string, ExchangeRate> _rates = new();

	protected object RefreshLock { get; } = new();
	public DateTimeOffset CurrenciesLastUpdated { get; protected set; } = new();
	public ImmutableSortedSet<string> Currencies { get; protected set; } = ImmutableSortedSet<string>.Empty;

	public virtual async Task<List<string>> QuerySupportedCurrenciesAsync(EndPoint? torEndpoint, CancellationToken cancellationToken)
	{
		if (AutoCurrencyRefresh)
		{
			var rates = await QueryRateAsync("USD", torEndpoint, cancellationToken);
			return rates?.Keys?.ToList() ?? [];
		}
		else
		{
			throw new NotImplementedException();
		}
	}

	public abstract Task<Dictionary<string, decimal>> QueryRateAsync(string currency, EndPoint? torEndpoint, CancellationToken cancellationToken);
}
