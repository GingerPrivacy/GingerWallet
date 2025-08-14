using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using WalletWasabi.Extensions;
using WalletWasabi.Lang.Models;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Lang;

public static class LocalizationExtension
{
	public static string GetSafeValue(this ResourceManager manager, string key)
	{
		return manager.GetString(key, Resources.Culture) ?? "";
	}

	public static string ToLocalTranslation(this DisplayLanguage language)
	{
		return language switch
		{
			DisplayLanguage.English => "English",
			DisplayLanguage.Spanish => "Español",
			DisplayLanguage.Hungarian => "Magyar",
			DisplayLanguage.French => "Français",
			DisplayLanguage.Chinese => "中文",
			DisplayLanguage.German => "Deutsch",
			DisplayLanguage.Portuguese => "Português",
			DisplayLanguage.Turkish => "Türkçe",
			DisplayLanguage.Italian => "Italiano",
			_ => language.ToString()
		};
	}

	public static string ToEscapeSequenceString(this string input)
	{
		return input
			.Replace("\\0", "\0")
			.Replace("\\n", "\n")
			.Replace("\\t", "\t")
			.Replace("\\r", "\r")
			.Replace("\\'", "\'")
			.Replace("\\\"", "\"");
	}

	public static string SafeInject(this string main, params object?[] texts)
	{
		try
		{
			return string.Format(Resources.Culture, main, texts);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}

		return "";
	}

	public static string GuessPreferredDecimalSeparator()
	{
		var allowedDecimalSeparators = Enum.GetValues(typeof(DecimalSeparator)).Cast<DecimalSeparator>().Select(x => x.GetChar()).ToArray();

		var osDecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
		if (allowedDecimalSeparators.Contains(osDecimalSeparator))
		{
			return osDecimalSeparator;
		}

		return GingerCultureInfo.DefaultDecimalSeparator;
	}

	public static string GuessPreferredGroupSeparator()
	{
		var allowedGroupSeparators = Enum.GetValues(typeof(GroupSeparator)).Cast<GroupSeparator>().Select(x => x.GetChar()).ToArray();

		var osGroupSeparator =
			CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator
				.Replace("\u202F", GroupSeparator.Space.GetChar()) // Narrow No-Break Space
				.Replace("\u00a0", GroupSeparator.Space.GetChar()) // No-Break Space
				.Replace("\u3000", GroupSeparator.Space.GetChar()) // Ideographic Space
				.Replace("\u2007", GroupSeparator.Space.GetChar()) // Figure Space
				.Replace("\u2008", GroupSeparator.Space.GetChar()); // Punctuation Space

		if (allowedGroupSeparators.Contains(osGroupSeparator))
		{
			return osGroupSeparator;
		}

		return GingerCultureInfo.DefaultGroupSeparator;
	}

	public static string GuessPreferredCurrencyCode(this CultureInfo selectedCulture, IEnumerable<string> allowedCurrencies)
	{
		try
		{
			var osSelectedCulture = CultureInfo.CurrentUICulture;
			if (selectedCulture.TwoLetterISOLanguageName == osSelectedCulture.TwoLetterISOLanguageName)
			{
				var osSelectedRegion = new RegionInfo(osSelectedCulture.Name);
				var osCurrency = osSelectedRegion.ISOCurrencySymbol;

				if (allowedCurrencies.Contains(osCurrency))
				{
					return osCurrency;
				}
			}

			var userSelectedRegion = new RegionInfo(selectedCulture.Name);
			var currency = userSelectedRegion.ISOCurrencySymbol;

			if (allowedCurrencies.Contains(currency))
			{
				return currency;
			}
		}
		catch
		{
			// Ignored.
		}

		return GingerCultureInfo.DefaultFiatCurrencyTicker;
	}

	public static int[] GetBitcoinFractionGroupSizes(this CultureInfo info)
	{
		if (info is GingerCultureInfo ei)
		{
			return ei.BitcoinFractionGroupSizes;
		}

		return GingerCultureInfo.DefaultBitcoinFractionSizes;
	}

	public static string GetBitcoinTicker(this CultureInfo info)
	{
		if (info is GingerCultureInfo ei)
		{
			return ei.BitcoinTicker;
		}

		return GingerCultureInfo.DefaultBitcoinCurrencyTicker;
	}

	public static string GetFiatTicker(this CultureInfo info)
	{
		if (info is GingerCultureInfo ei)
		{
			return ei.FiatTicker;
		}

		return GingerCultureInfo.DefaultFiatCurrencyTicker;
	}

	public static string[] ToKeywords(this string words)
	{
		return words.Replace(" ", "").Split(',');
	}
}
