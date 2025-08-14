using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.Extensions;

public static class CurrencyExtensions
{
	public static Money CalculateDestinationAmount(this BuildTransactionResult result, BitcoinAddress destination)
	{
		var isNormalPayment = result.OuterWalletOutputs.Any();

		if (isNormalPayment)
		{
			return result.OuterWalletOutputs.Sum(x => x.Amount);
		}

		return result.InnerWalletOutputs
			.Where(x => x.ScriptPubKey == destination.ScriptPubKey)
			.Select(x => x.Amount)
			.Sum();
	}

	public static string FormattedBtcFixedFractional(this decimal amount)
	{
		return Money.Coins(amount).ToFormattedString();
	}

	public static string FormattedFiat(this decimal amount, string format = "N2")
	{
		return amount.ToString(format, Resources.Culture.NumberFormat).Trim();
	}

	public static decimal BtcToFiat(this Money money, decimal exchangeRate)
	{
		return money.ToDecimal(MoneyUnit.BTC) * exchangeRate;
	}

	public static string ToFiatAprox(this decimal n) => n != decimal.Zero ? $"≈{ToFiatFormatted(n)}" : "";

	public static string ToFiatAproxBetweenParens(this decimal n) => n != decimal.Zero ? $"({ToFiatAprox(n)})" : "";

	public static string ToFiatFormatted(this decimal n)
	{
		var ticker = Resources.Culture.GetFiatTicker();
		return ToFiatAmountFormatted(n) + " " + ticker;
	}

	public static string ToFiatAmountFormatted(this decimal n)
	{
		return n switch
		{
			>= 10 => Math.Ceiling(n).ToString("N0", Resources.Culture.NumberFormat),
			>= 1 => n.ToString("N1", Resources.Culture.NumberFormat),
			_ => n.ToString("N2", Resources.Culture.NumberFormat)
		};
	}

	public static string ToFormattedFiat(this decimal n, string? currencyString = null)
	{
		var strNum = n.ToString("#,0.##", Resources.Culture.NumberFormat);
		return string.IsNullOrEmpty(currencyString) ? strNum : $"{strNum} {currencyString}";
	}

	public static decimal WithFriendlyDecimals(this double n)
	{
		return WithFriendlyDecimals((decimal)n);
	}

	public static decimal WithFriendlyDecimals(this decimal n)
	{
		return Math.Abs(n) switch
		{
			>= 10 => decimal.Round(n),
			>= 1 => decimal.Round(n, 1),
			_ => decimal.Round(n, 2)
		};
	}

	public static string ToFeeDisplayUnitRawString(this Money? fee)
	{
		if (fee is null)
		{
			return Resources.Unknown;
		}

		var displayUnit = Services.UiConfig.FeeDisplayUnit.GetEnumValueOrDefault(FeeDisplayUnit.BTC);

		return displayUnit switch
		{
			FeeDisplayUnit.Satoshis => fee.Satoshi.ToString(Resources.Culture.NumberFormat),
			_ => fee.ToString(Resources.Culture.NumberFormat)
		};
	}

	public static string ToFeeDisplayUnitFormattedString(this Money? fee)
	{
		if (fee is null)
		{
			return Resources.Unknown;
		}

		var displayUnit = Services.UiConfig.FeeDisplayUnit.GetEnumValueOrDefault(FeeDisplayUnit.BTC);
		var moneyUnit = displayUnit.ToMoneyUnit();

		var feePartText = moneyUnit switch
		{
			MoneyUnit.BTC => fee.ToFormattedString(),
			MoneyUnit.Satoshi => fee.Satoshi.ToString(Resources.Culture.NumberFormat),
			_ => fee.ToString(Resources.Culture.NumberFormat)
		};

		var feeText = $"{feePartText} {displayUnit.FriendlyName()}";

		return feeText;
	}

	public static MoneyUnit ToMoneyUnit(this FeeDisplayUnit feeDisplayUnit) =>
		feeDisplayUnit switch
		{
			FeeDisplayUnit.BTC => MoneyUnit.BTC,
			FeeDisplayUnit.Satoshis => MoneyUnit.Satoshi,
			_ => throw new InvalidOperationException($"Invalid Fee Display Unit value: {feeDisplayUnit}")
		};
}
