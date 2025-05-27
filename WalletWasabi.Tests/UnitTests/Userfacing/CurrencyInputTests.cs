using System.Collections.Generic;
using System.Globalization;
using WalletWasabi.Userfacing;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Userfacing;

public class CurrencyInputTests
{
	[Theory]
	[InlineData(".", ",")]
	public void CorrectAmountText(string correctSeparator, string wrongSeparator)
	{
		var testCases = CurrencyTestCases(correctSeparator, wrongSeparator);

		foreach (var testCase in testCases)
		{
			var result = CurrencyInput.TryCorrectAmount(testCase.Amount, out var correction);
			Assert.Equal(testCase.ExpectedCorrection, correction);
			Assert.Equal(testCase.ExpectedResult, result);
		}
	}

	[Theory]
	[InlineData(".", ",")]
	public void CorrectBitcoinAmountText(string correctSeparator, string wrongSeparator)
	{
		var testCases = BitcoinTestCases(correctSeparator, wrongSeparator);

		foreach (var testCase in testCases)
		{
			var result = CurrencyInput.TryCorrectBitcoinAmount(testCase.Amount, out var correction);
			Assert.Equal(testCase.ExpectedCorrection, correction);
			Assert.Equal(testCase.ExpectedResult, result);
		}
	}

	public record TestCase(string Amount, bool ExpectedResult, string? ExpectedCorrection);

	public List<TestCase> CurrencyTestCases(string correctSeparator, string wrongSeparator)
	{
		Lang.Resources.Culture = new CultureInfo("en-US")
		{
			NumberFormat =
				{
					CurrencyGroupSeparator = " ",
					CurrencyDecimalSeparator = correctSeparator,
					NumberGroupSeparator = " ",
					NumberDecimalSeparator = correctSeparator
				}
		};

		List<TestCase> result = [
			new("1", false, null),
			new($"1{correctSeparator}", false, null),
			new($"1{correctSeparator}0", false, null),
			new("", false, null),
			new($"0{correctSeparator}0", false, null),
			new("0", false, null),
			new($"0{correctSeparator}", false, null),
			new($"{correctSeparator}1", false, null),
			new($"{correctSeparator}", false, null),
			new($"{wrongSeparator}", true, $"{correctSeparator}"),
			new("20999999", false, null),
			new($"2{correctSeparator}1", false, null),
			new($"1{correctSeparator}11111111", false, null),
			new($"1{correctSeparator}00000001", false, null),
			new($"20999999{correctSeparator}9769", false, null),
			new(" ", true, ""),
			new("  ", true, ""),
			new("abc", true, ""),
			new("1a", true, "1"),
			new("a1a", true, "1"),
			new("a1 a", true, "1"),
			new("a2 1 a", true, "21"),
			new($"2{wrongSeparator}1", true, $"2{correctSeparator}1"),
			new("2٫1", true, $"2{correctSeparator}1"),
			new("2٬1", true, $"2{correctSeparator}1"),
			new("2⎖1", true, $"2{correctSeparator}1"),
			new("2·1", true, $"2{correctSeparator}1"),
			new("2'1", true, $"2{correctSeparator}1"),
			new($"2{correctSeparator}1{correctSeparator}", true, $"2{correctSeparator}1"),
			new($"2{correctSeparator}1{correctSeparator}{correctSeparator}", true, $"2{correctSeparator}1"),
			new($"2{correctSeparator}1{correctSeparator}{wrongSeparator}{correctSeparator}", true, $"2{correctSeparator}1"),
			new($"2{correctSeparator}1{correctSeparator} {correctSeparator} {correctSeparator}", true, $"2{correctSeparator}1"),
			new($"2{correctSeparator}1{correctSeparator}1", true, ""),
			new($"2{wrongSeparator}1{correctSeparator}1", true, ""),
			new($"{correctSeparator}1{correctSeparator}", true, $"{correctSeparator}1"),
			new($"{wrongSeparator}1", true, $"{correctSeparator}1"),
			new($"{correctSeparator}{correctSeparator}1", true, $"{correctSeparator}1"),
			new($"{correctSeparator}{wrongSeparator}1", true, $"{correctSeparator}1"),
			new("01", true, "1"),
			new("001", true, "1"),
			new($"001{correctSeparator}0", true, $"1{correctSeparator}0"),
			new($"001{correctSeparator}00", true, $"1{correctSeparator}00"),
			new("00", true, "0"),
			new("0  0", true, "0"),
			new($"001{correctSeparator}", true, $"1{correctSeparator}"),
			new($"00{correctSeparator}{correctSeparator}{correctSeparator}{correctSeparator}1{correctSeparator}{correctSeparator}{correctSeparator}{wrongSeparator}a", true, ""),
			new($"0{correctSeparator}{correctSeparator}{correctSeparator}1{correctSeparator}", true, ""),
			new($"1{correctSeparator}{correctSeparator}{correctSeparator}1{correctSeparator}", true, ""),
			new($"1{correctSeparator}s{correctSeparator}1{correctSeparator}{correctSeparator}{correctSeparator}1{correctSeparator}", true, ""),
			// Negative values.
			new("-0", true, "0"),
			new("-1", true, "1"),
			new($"-0{correctSeparator}5", true, $"0{correctSeparator}5"),
			new("-0,5", true, $"0{correctSeparator}5")
		];
		return result;
	}

	public List<TestCase> BitcoinTestCases(string correctSeparator, string wrongSeparator)
	{
		var result = CurrencyTestCases(correctSeparator, wrongSeparator);

		result.AddRange([
			new($"1{correctSeparator}000000000", true, $"1{correctSeparator}00000000"),
			new($"1{correctSeparator}111111119", true, $"1{correctSeparator}11111111"),
			new($"20999999{correctSeparator}97690001", false, null),
			new("30999999", false, null),
			new("303333333333333333999999", false, null),
			new($"20999999{correctSeparator}977", false, null),
			new($"209999990{correctSeparator}9769", false, null),
			new($"20999999{correctSeparator}976910000000000", true, $"20999999{correctSeparator}97691000"),
			new($"209999990000000000{correctSeparator}97000000000069", true, $"209999990000000000{correctSeparator}97000000"),
			new($"1{correctSeparator}000000001", true, $"1{correctSeparator}00000000"),
			new($"20999999{correctSeparator}97000000000069", true, $"20999999{correctSeparator}97000000")
		]);
		return result;
	}
}
