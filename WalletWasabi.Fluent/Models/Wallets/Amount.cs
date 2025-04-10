using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Models.Wallets;

/// <summary>
/// Encapsulates a BTC amount and its corresponding Fiat exchange rate as an Observable sequence.
/// </summary>
public class Amount
{
	public static readonly Amount Zero = new();

	/// <summary>
	/// Private constructor to initialize Zero value
	/// </summary>
	private Amount()
	{
		Btc = Money.Zero;
		Fiat = Observable.Return(0m);
		HasFiatBalance = Observable.Return(false);
	}

	public Amount(Money money, IAmountProvider exchangeRateProvider)
	{
		Btc = money;
		Fiat = exchangeRateProvider.ExchangeRateObservable.Select(x => x * Btc.ToDecimal(MoneyUnit.BTC));
		HasFiatBalance = Fiat.Select(x => x != 0m);
	}

	public Amount(decimal amount, IAmountProvider exchangeRateProvider)
	{
		Btc = Money.Coins(amount);
		Fiat = exchangeRateProvider.ExchangeRateObservable.Select(x => x * Btc.ToDecimal(MoneyUnit.BTC));
		HasFiatBalance = Fiat.Select(x => x != 0m);
	}

	public Amount(decimal amount)
	{
		Btc = Money.Coins(amount);
		Fiat = Observable.Return(0m);
		HasFiatBalance = Observable.Return(false);
	}

	public Money Btc { get; }

	public IObservable<decimal> Fiat { get; }

	public bool HasBalance => Btc != Money.Zero;

	public IObservable<bool> HasFiatBalance { get; }

	public string FormattedBtc => Btc.ToFormattedString();
	public string FormattedBtcWithUnit => Btc.ToFormattedString(addTicker: true);
}
