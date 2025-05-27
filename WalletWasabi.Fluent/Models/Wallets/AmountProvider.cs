using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Models.Wallets;

[AppLifetime]
public partial class AmountProvider : ReactiveObject
{
	private readonly ExchangeRateService _exchangeRateService;

	[AutoNotify] private decimal _exchangeRate;
	[AutoNotify] private IOrderedEnumerable<string> _supportedCurrencies = Enumerable.Empty<string>().OrderBy(x => x);

	public AmountProvider(ExchangeRateService exchangeRateService)
	{
		_exchangeRateService = exchangeRateService;

		ExchangeRateObservable = Observable
			.FromEventPattern<decimal>(_exchangeRateService, nameof(_exchangeRateService.ExchangeRateChanged))
			.Select(x => x.EventArgs)
			.StartWith(exchangeRateService.ExchangeRate?.Value ?? 0)
			.ObserveOn(RxApp.MainThreadScheduler)
			.ReplayLastActive();

		ExchangeRateObservable.BindTo(this, x => x.ExchangeRate);

		SupportedCurrenciesObservable = Observable
			.FromEventPattern<ImmutableSortedSet<string>>(_exchangeRateService, nameof(_exchangeRateService.SupportedCurrenciesChanged))
			.Select(x => x.EventArgs)
			.StartWith(exchangeRateService.SupportedCurrencies)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(x => x.OrderBy(y => y))
			.ReplayLastActive();

		SupportedCurrenciesObservable.BindTo(this, x => x.SupportedCurrencies);
	}

	public IObservable<decimal> ExchangeRateObservable { get; }

	public IObservable<IOrderedEnumerable<string>> SupportedCurrenciesObservable { get; }

	public Amount Create(Money? money)
	{
		return new Amount(money ?? Money.Zero, this);
	}

	public Amount Create(decimal value)
	{
		return new Amount(value, this);
	}
}
