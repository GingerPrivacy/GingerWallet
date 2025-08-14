using Avalonia.Data.Converters;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Converters;

public static class MoneyConverters
{
	public static readonly IValueConverter ToFiatFormatted =
		new FuncValueConverter<decimal, string>(n => n.ToFiatFormatted());

	public static readonly IValueConverter ToFiatNumber =
		new FuncValueConverter<Money, string?>(n => n?.ToDecimal(MoneyUnit.BTC).WithFriendlyDecimals().ToString(Lang.Resources.Culture.NumberFormat));

	public static readonly IValueConverter ToFiatAmountFormattedWithoutSpaces =
		new FuncValueConverter<decimal, string>(n => n.ToFiatAmountFormatted().Replace(" ", ""));

	public static readonly IValueConverter ToFiatApprox =
		new FuncValueConverter<decimal, string>(n => n.ToFiatAprox());

	public static readonly IValueConverter ToFiatApproxBetweenParens =
		new FuncValueConverter<decimal, string>(n => n.ToFiatAproxBetweenParens());

	public static readonly IValueConverter ToBtc =
		new FuncValueConverter<Money, string?>(n => n?.ToFormattedString(addTicker: true));

	public static readonly IValueConverter ToFeeWithUnit =
		new FuncValueConverter<Money, string?>(n => n?.ToFeeDisplayUnitFormattedString());

	public static readonly IValueConverter ToFeeWithoutUnit =
		new FuncValueConverter<Money?, string?>(n => n?.ToFeeDisplayUnitRawString());

	public static readonly IValueConverter PercentageDifferenceConverter =
		new FuncValueConverter<double, string>(TextHelpers.FormatPercentageDiff);
}
