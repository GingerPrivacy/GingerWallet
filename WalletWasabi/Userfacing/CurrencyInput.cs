using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;

namespace WalletWasabi.Userfacing;

public static partial class CurrencyInput
{
	[GeneratedRegex(@"[\d.,٫٬⎖·\']")]
	public static partial Regex RegexValidCharsOnly();

	[GeneratedRegex(@"^[\d.,'٬٫⎖·  ˙’]+$")]
	public static partial Regex RegexValidNumber();

	public static bool TryCorrectAmount(string original, [NotNullWhen(true)] out string? best)
	{
		var decimalSeparator = Lang.Resources.Culture.NumberFormat.NumberDecimalSeparator;
		var decimalSeparatorChar = decimalSeparator[0];

		var corrected = original;

		// Correct amount
		Regex digitsOnly = new(@"[^\d.,٫٬⎖·\']");

		// Make it digits and .,٫٬⎖·\ only.
		corrected = digitsOnly.Replace(corrected, "");

		// https://en.wikipedia.org/wiki/Decimal_separator
		corrected = corrected.Replace(",", decimalSeparator);
		corrected = corrected.Replace(".", decimalSeparator);
		corrected = corrected.Replace("٫", decimalSeparator);
		corrected = corrected.Replace("٬", decimalSeparator);
		corrected = corrected.Replace("⎖", decimalSeparator);
		corrected = corrected.Replace("·", decimalSeparator);
		corrected = corrected.Replace("'", decimalSeparator);

		// Trim trailing dots except the last one.
		if (corrected.EndsWith(decimalSeparatorChar))
		{
			corrected = $"{corrected.TrimEnd(decimalSeparatorChar)}{decimalSeparatorChar}";
		}

		// Trim starting zeros.
		if (corrected.StartsWith('0'))
		{
			// If zeroless starts with a dot, then leave a zero.
			// Else trim all the zeros.
			var zeroless = corrected.TrimStart('0');
			if (zeroless.Length == 0)
			{
				corrected = "0";
			}
			else if (zeroless.StartsWith(decimalSeparatorChar))
			{
				corrected = $"0{corrected.TrimStart('0')}";
			}
			else
			{
				corrected = corrected.TrimStart('0');
			}
		}

		// Trim leading dots except the first one.
		if (corrected.StartsWith(decimalSeparatorChar))
		{
			corrected = $"{decimalSeparatorChar}{corrected.TrimStart(decimalSeparatorChar)}";
		}

		// Do not enable having more than one dot.
		if (corrected.Count(x => x == decimalSeparatorChar) > 1)
		{
			// Except if it's at the end, we just remove it.
			corrected = corrected.TrimEnd(decimalSeparatorChar);
			if (corrected.Count(x => x == decimalSeparatorChar) > 1)
			{
				corrected = "";
			}
		}

		if (corrected != original)
		{
			best = corrected;
			return true;
		}

		best = null;
		return false;
	}

	public static bool TryCorrectBitcoinAmount(string original, [NotNullWhen(true)] out string? best)
	{
		TryCorrectAmount(original, out var corrected);

		// If the original value wasn't fixed, it's definitely not a null.
		corrected ??= original;

		// Enable max 8 decimals.
		var dotIndex = corrected.IndexOf(Lang.Resources.Culture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal);
		if (dotIndex != -1 && corrected.Length - (dotIndex + 1) > 8)
		{
			corrected = corrected[..(dotIndex + 1 + 8)];
		}

		if (corrected != original)
		{
			best = corrected;
			return true;
		}

		best = null;
		return false;
	}
}
