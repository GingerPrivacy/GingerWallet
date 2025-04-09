using System.Collections.Generic;
using System.Globalization;
using WalletWasabi.Userfacing;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Userfacing;

public class CurrencyInputTests
{
	[Theory]
	[MemberData(nameof(GetCurrencyTestDataWithDot))]
	public void CorrectAmountText(CultureInfo culture, string amount, bool expectedResult, string expectedCorrection)
	{
		Lang.Resources.Culture = culture;
		var result = CurrencyInput.TryCorrectAmount(amount, out var correction);
		Assert.Equal(expectedCorrection, correction);
		Assert.Equal(expectedResult, result);
	}

	[Theory]
	[MemberData(nameof(GetBitcoinTestDataWithDot))]
	public void CorrectBitcoinAmountText(CultureInfo culture, string amount, bool expectedResult, string expectedCorrection)
	{
		Lang.Resources.Culture = culture;
		var result = CurrencyInput.TryCorrectBitcoinAmount(amount, out var correction);
		Assert.Equal(expectedCorrection, correction);
		Assert.Equal(expectedResult, result);
	}

	public static IEnumerable<object[]> GetBitcoinTestDataWithDot()
	{
		return new BitcoinTestData(".", ",");
	}

	// public static IEnumerable<object[]> GetBitcoinTestDataWithComma()
	// {
	// 	return new BitcoinTestData(",", ".");
	// }

	public static IEnumerable<object[]> GetCurrencyTestDataWithDot()
	{
		return new CurrencyTestData(".", ",");
	}

	// public static IEnumerable<object[]> GetCurrencyTestDataWithComma()
	// {
	// 	return new CurrencyTestData(",", ".");
	// }

	private class CurrencyTestData : TheoryData<CultureInfo, string, bool, string?>
	{
		protected CultureInfo Culture { get; set; }

		public CurrencyTestData(string correctSeparator, string wrongSeparator)
		{
			Culture = new CultureInfo("en-US")
			{
				NumberFormat =
				{
					CurrencyGroupSeparator = " ",
					CurrencyDecimalSeparator = correctSeparator,
					NumberGroupSeparator = " ",
					NumberDecimalSeparator = correctSeparator
				}
			};

			Add(Culture, "1", false, null);
			Add(Culture, $"1{correctSeparator}", false, null);
			Add(Culture, $"1{correctSeparator}0", false, null);
			Add(Culture, "", false, null);
			Add(Culture, $"0{correctSeparator}0", false, null);
			Add(Culture, "0", false, null);
			Add(Culture, $"0{correctSeparator}", false, null);
			Add(Culture, $"{correctSeparator}1", false, null);
			Add(Culture, $"{correctSeparator}", false, null);
			Add(Culture, $"{wrongSeparator}", true, $"{correctSeparator}");
			Add(Culture, "20999999", false, null);
			Add(Culture, $"2{correctSeparator}1", false, null);
			Add(Culture, $"1{correctSeparator}11111111", false, null);
			Add(Culture, $"1{correctSeparator}00000001", false, null);
			Add(Culture, $"20999999{correctSeparator}9769", false, null);
			Add(Culture, " ", true, "");
			Add(Culture, "  ", true, "");
			Add(Culture, "abc", true, "");
			Add(Culture, "1a", true, "1");
			Add(Culture, "a1a", true, "1");
			Add(Culture, "a1 a", true, "1");
			Add(Culture, "a2 1 a", true, "21");
			Add(Culture, $"2{wrongSeparator}1", true, $"2{correctSeparator}1");
			Add(Culture, "2٫1", true, $"2{correctSeparator}1");
			Add(Culture, "2٬1", true, $"2{correctSeparator}1");
			Add(Culture, "2⎖1", true, $"2{correctSeparator}1");
			Add(Culture, "2·1", true, $"2{correctSeparator}1");
			Add(Culture, "2'1", true, $"2{correctSeparator}1");
			Add(Culture, $"2{correctSeparator}1{correctSeparator}", true, $"2{correctSeparator}1");
			Add(Culture, $"2{correctSeparator}1{correctSeparator}{correctSeparator}", true, $"2{correctSeparator}1");
			Add(Culture, $"2{correctSeparator}1{correctSeparator}{wrongSeparator}{correctSeparator}", true, $"2{correctSeparator}1");
			Add(Culture, $"2{correctSeparator}1{correctSeparator} {correctSeparator} {correctSeparator}", true, $"2{correctSeparator}1");
			Add(Culture, $"2{correctSeparator}1{correctSeparator}1", true, "");
			Add(Culture, $"2{wrongSeparator}1{correctSeparator}1", true, "");
			Add(Culture, $"{correctSeparator}1{correctSeparator}", true, $"{correctSeparator}1");
			Add(Culture, $"{wrongSeparator}1", true, $"{correctSeparator}1");
			Add(Culture, $"{correctSeparator}{correctSeparator}1", true, $"{correctSeparator}1");
			Add(Culture, $"{correctSeparator}{wrongSeparator}1", true, $"{correctSeparator}1");
			Add(Culture, "01", true, "1");
			Add(Culture, "001", true, "1");
			Add(Culture, $"001{correctSeparator}0", true, $"1{correctSeparator}0");
			Add(Culture, $"001{correctSeparator}00", true, $"1{correctSeparator}00");
			Add(Culture, "00", true, "0");
			Add(Culture, "0  0", true, "0");
			Add(Culture, $"001{correctSeparator}", true, $"1{correctSeparator}");
			Add(Culture, $"00{correctSeparator}{correctSeparator}{correctSeparator}{correctSeparator}1{correctSeparator}{correctSeparator}{correctSeparator}{wrongSeparator}a", true, "");
			Add(Culture, $"0{correctSeparator}{correctSeparator}{correctSeparator}1{correctSeparator}", true, "");
			Add(Culture, $"1{correctSeparator}{correctSeparator}{correctSeparator}1{correctSeparator}", true, "");
			Add(Culture, $"1{correctSeparator}s{correctSeparator}1{correctSeparator}{correctSeparator}{correctSeparator}1{correctSeparator}", true, "");

			// Negative values.
			Add(Culture, "-0", true, "0");
			Add(Culture, "-1", true, "1");
			Add(Culture, $"-0{correctSeparator}5", true, $"0{correctSeparator}5");
			Add(Culture, "-0,5", true, $"0{correctSeparator}5");
		}
	}

	private class BitcoinTestData : CurrencyTestData
	{
		public BitcoinTestData(string correctSeparator, string wrongSeparator) : base(correctSeparator, wrongSeparator)
		{
			Add(Culture, $"1{correctSeparator}000000000", true, $"1{correctSeparator}00000000");
			Add(Culture, $"1{correctSeparator}111111119", true, $"1{correctSeparator}11111111");
			Add(Culture, $"20999999{correctSeparator}97690001", false, null);
			Add(Culture, "30999999", false, null);
			Add(Culture, "303333333333333333999999", false, null);
			Add(Culture, $"20999999{correctSeparator}977", false, null);
			Add(Culture, $"209999990{correctSeparator}9769", false, null);
			Add(Culture, $"20999999{correctSeparator}976910000000000", true, $"20999999{correctSeparator}97691000");
			Add(Culture, $"209999990000000000{correctSeparator}97000000000069", true, $"209999990000000000{correctSeparator}97000000");
			Add(Culture, $"1{correctSeparator}000000001", true, $"1{correctSeparator}00000000");
			Add(Culture, $"20999999{correctSeparator}97000000000069", true, $"20999999{correctSeparator}97000000");
		}
	}
}
