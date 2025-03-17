using System.Globalization;
using Avalonia.Data.Converters;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Converters;


/*
 * TODO
 * Remove this class completely!!
 */
public static class MoneyConverters
{
	private static readonly string Ticker = "";

	static MoneyConverters()
	{
		Ticker = Services.Config.ExchangeCurrency;
	}

	public static readonly IValueConverter ToFiatFormatted =
		new FuncValueConverter<decimal, string>(n => n.ToFiatFormatted(Ticker));

	public static readonly IValueConverter ToFiatNumber =
		new FuncValueConverter<Money, string?>(n => n?.ToDecimal(MoneyUnit.BTC).WithFriendlyDecimals().ToString(CultureInfo.InvariantCulture));

	public static readonly IValueConverter ToFiatAmountFormattedWithoutSpaces =
		new FuncValueConverter<decimal, string>(n => n.ToFiatAmountFormatted().Replace(" ", ""));

	public static readonly IValueConverter ToFiatApprox =
		new FuncValueConverter<decimal, string>(n => n.ToFiatAprox(Ticker));

	public static readonly IValueConverter ToFiatApproxBetweenParens =
		new FuncValueConverter<decimal, string>(n => n.ToFiatAproxBetweenParens(Ticker));

	public static readonly IValueConverter ToBtc =
		new FuncValueConverter<Money, string?>(n => n?.ToBtcWithUnit());

	public static readonly IValueConverter ToFeeWithUnit =
		new FuncValueConverter<Money, string?>(n => n?.ToFeeDisplayUnitFormattedString());

	public static readonly IValueConverter ToFeeWithoutUnit =
		new FuncValueConverter<Money?, string?>(n => n?.ToFeeDisplayUnitRawString());

	public static readonly IValueConverter PercentageDifferenceConverter =
			new FuncValueConverter<double, string>(TextHelpers.FormatPercentageDiff );
}
