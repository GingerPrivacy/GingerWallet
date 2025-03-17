using System.Linq;
using NBitcoin;
using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Converters;
using WalletWasabi.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class AmountProvider : ReactiveObject
{
	private readonly WasabiSynchronizer _synchronizer;
	[AutoNotify] private decimal _exchangeRate;

	public AmountProvider(WasabiSynchronizer synchronizer, string ticker)
	{
		_synchronizer = synchronizer;
		Ticker = ticker;
		ExchangeRateObservable =
			this.WhenAnyValue(provider => provider._synchronizer.ExchangeRates)
				.Select(x => x.FirstOrDefault(y => y.Ticker == Ticker)?.Rate ?? 0)
				.ObserveOn(RxApp.MainThreadScheduler);

		ExchangeRateObservable.BindTo(this, x => x.ExchangeRate);
	}

	public IObservable<decimal> ExchangeRateObservable { get; }

	public string Ticker { get; }

	public string[] SupportedCurrencies => _synchronizer.SupportedCurrencies;

	public Amount Create(Money? money)
	{
		return new Amount(money ?? Money.Zero, this);
	}
}
