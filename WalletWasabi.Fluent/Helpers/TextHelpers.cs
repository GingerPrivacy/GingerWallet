using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.Helpers;

public static partial class TextHelpers
{
	public static string AddSIfPlural(int n) => n > 1 ? Resources.Plural : "";

	private static string ConcatNumberAndUnit(int n, string unit) => n > 0 ? $"{n} {unit}{AddSIfPlural(n)}" : "";

	[GeneratedRegex(@"\s+")]
	private static partial Regex ParseLabelRegex();

	private static void AddIfNotEmpty(List<string> list, string item)
	{
		if (!string.IsNullOrEmpty(item))
		{
			list.Add(item);
		}
	}

	public static string TimeSpanToFriendlyString(TimeSpan time)
	{
		var textMembers = new List<string>();
		string result = "";

		AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Days, Resources.day));
		AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Hours, Resources.hour));
		AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Minutes, Resources.minute));
		AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Seconds, Resources.second));

		for (int i = 0; i < textMembers.Count; i++)
		{
			result += textMembers[i];

			if (textMembers.Count > 1 && i < textMembers.Count - 2)
			{
				result += ", ";
			}
			else if (textMembers.Count > 1 && i == textMembers.Count - 2)
			{
				result += $" {Resources.and} ";
			}
		}

		return result;
	}

	/*
	 * NBitcoin uses InvariantCulture hardcoded in the ToString() method,
	 * so here we have to correct it the culture set by the user.
	 */
	public static string ToString(this Money money, NumberFormatInfo formatInfo, bool fplus = false, bool trimExcessZero = false)
	{
		return money.ToString(fplus: fplus, trimExcessZero).Replace(".", formatInfo.NumberDecimalSeparator);
	}

	/*
	 * NBitcoin uses InvariantCulture hardcoded,
	 * so we have to make the input eatable for it...
	 */
	public static string PrepareForMoneyParsing(this string text)
	{
		return text.Replace(Resources.Culture.NumberFormat.NumberDecimalSeparator, ".");
	}

	public static string ToFormattedString(
		this Money money,
		bool fplus = false,
		bool trimExcessZero = false,
		int[]? fractionGrouping = null,
		bool addTicker = false)
	{
		var moneyString = money.ToString(Resources.Culture.NumberFormat, fplus: fplus, trimExcessZero: trimExcessZero);
		var hasSign = moneyString.StartsWith("+") || moneyString.StartsWith("-");
		var sign = hasSign ? moneyString[0] : '\0';
		var numericPart = hasSign ? moneyString.Substring(1) : moneyString;
		var parts = numericPart.Split(Resources.Culture.NumberFormat.NumberDecimalSeparator);

		var wholePartGrouping = Resources.Culture.NumberFormat.NumberGroupSizes;
		var pos = 0;
		var wholePartReversed = parts[0].Reverse().Select(x => x.ToString()).ToList();
		for (var i = 0; i < wholePartGrouping.Length && pos < wholePartReversed.Count; i++)
		{
			pos += wholePartGrouping[i];
			if (pos < wholePartReversed.Count)
			{
				wholePartReversed.Insert(pos, Resources.Culture.NumberFormat.NumberGroupSeparator);
				pos += Resources.Culture.NumberFormat.NumberGroupSeparator.Length;
			}
		}

		var wholePart = new string(string.Concat(wholePartReversed.AsEnumerable().Reverse()));

		var finalString = (hasSign ? sign.ToString() : "") + wholePart;

		if (parts.Length == 2)
		{
			var fractionPart = parts[1];
			fractionGrouping ??= Resources.Culture.GetBitcoinFractionGroupSizes();

			pos = 0;
			foreach (var group in fractionGrouping)
			{
				pos += group;
				if (pos < fractionPart.Length)
				{
					fractionPart = fractionPart.Insert(pos, UiConstants.BitcoinGroupSeparator);
					pos += UiConstants.BitcoinGroupSeparator.Length;
				}
			}

			finalString += Resources.Culture.NumberFormat.CurrencyDecimalSeparator + fractionPart;
		}

		return finalString + (addTicker ? $" {Resources.Culture.GetBitcoinTicker()}" : "");
	}

	public static string ParseLabel(this string text) => ParseLabelRegex().Replace(text, " ").Trim();

	public static string TotalTrim(this string text)
	{
		return text
			.Replace("\r", "")
			.Replace("\n", "")
			.Replace("\t", "")
			.Replace(" ", "");
	}

	public static string ClearNumberFormat(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return "";
		}

		// Remove all spaces
		input = input.Replace(" ", "");

		StringBuilder result = new StringBuilder();
		int lastNonDigitIndex = -1;

		// Iterate through the string
		for (int i = 0; i < input.Length; i++)
		{
			if (char.IsDigit(input[i]))
			{
				result.Append(input[i]);
			}
			else
			{
				lastNonDigitIndex = result.Length;
			}
		}

		// If there's a non-digit character found, replace it with the decimal separator
		if (lastNonDigitIndex != -1)
		{
			result.Insert(lastNonDigitIndex, Lang.Resources.Culture.NumberFormat.NumberDecimalSeparator);
		}

		return result.ToString();
	}

	public static string GetConfirmationText(int confirmations)
	{
		return Resources.ConfirmedWithConfirmationCount.SafeInject(confirmations, AddSIfPlural(confirmations));
	}

	public static string FormatPercentageDiff(double n)
	{
		var precision = 0.01m;
		var withFriendlyDecimals = (n * 100).WithFriendlyDecimals();

		if (Math.Abs(withFriendlyDecimals) < precision)
		{
			var num = n >= 0 ? precision : -precision;
			return $"{Resources.LessThan} {num.ToString("+0.##;-0.##", Resources.Culture.NumberFormat)}%";
		}
		else
		{
			return $"{withFriendlyDecimals.ToString("+0.##;-0.##", Resources.Culture.NumberFormat)}%";
		}
	}
}
