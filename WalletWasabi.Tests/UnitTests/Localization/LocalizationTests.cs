using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using WalletWasabi.Extensions;
using WalletWasabi.Lang;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Localization;

public class LocalizationTests
{
	private readonly Regex _placeholderRegex = new(@"{\d+}", RegexOptions.Compiled);

	[Fact]
	public void SafeInjectTest()
	{
		var main = "This {0} a text.";
		var completeText = main.SafeInject("is");
		Assert.Equal("This is a text.", completeText);

		main = "This {0}} a text.";
		completeText = main.SafeInject("is");
		Assert.Equal("", completeText);
	}

	[Fact]
	public void ConsistentLeadingAndTrailingSpacesTest()
	{
		var supportedLanguages = Enum.GetValues(typeof(DisplayLanguage)).Cast<DisplayLanguage>().Select(x => x.GetDescription() ?? throw new InvalidOperationException("Missing Description"));

		var allValue = new Dictionary<object, List<string>>();

		foreach (var lang in supportedLanguages)
		{
			ResourceSet? resourceSet = Resources.ResourceManager.GetResourceSet(new CultureInfo(lang), true, true);

			if (resourceSet is null)
			{
				throw new InvalidOperationException($"Resource Set is not available for {lang}.");
			}

			foreach (DictionaryEntry entry in resourceSet)
			{
				var value = entry.Value?.ToString() ?? "";
				var key = entry.Key;

				if (allValue.ContainsKey(key) && allValue.TryGetValue(key, out var values))
				{
					values.Add(value);
				}
				else
				{
					allValue.Add(key, [value]);
				}
			}
		}

		foreach (var (key, values) in allValue)
		{
			var numberOfLeadingSpaces = -1;
			var numberOfTrailingSpaces = -1;

			foreach (string value in values)
			{
				var leading = value.TakeWhile(c => c == ' ').Count();
				var trailing = value.Reverse().TakeWhile(c => c == ' ').Count();

				if (numberOfLeadingSpaces == -1)
				{
					numberOfLeadingSpaces = leading;
				}
				else
				{
					Assert.True(numberOfLeadingSpaces == leading, $"Invalid leading spaces for '{key}' in value: '{value}'");
				}

				if (numberOfTrailingSpaces == -1)
				{
					numberOfTrailingSpaces = trailing;
				}
				else
				{
					Assert.True(numberOfTrailingSpaces == trailing, $"Invalid trailing spaces for '{key}' in value: '{value}'");
				}
			}
		}
	}


	[Fact]
	public void ResourceFormatTest()
	{
		var supportedLanguages = Enum.GetValues(typeof(DisplayLanguage)).Cast<DisplayLanguage>().Select(x => x.GetDescription() ?? throw new InvalidOperationException("Missing Description"));

		foreach (var lang in supportedLanguages)
		{
			ResourceSet? resourceSet = Resources.ResourceManager.GetResourceSet(new CultureInfo(lang), true, true);

			if (resourceSet is null)
			{
				throw new InvalidOperationException($"Resource Set is not available for {lang}.");
			}

			foreach (DictionaryEntry entry in resourceSet)
			{
				if (entry.Value is string value)
				{
					Assert.True(IsValidFormat(value), $"Invalid format in key '{entry.Key}' for culture: {lang}");
				}
			}
		}
	}

	private bool IsValidFormat(string value)
	{
		try
		{
			var matches = _placeholderRegex.Matches(value);

			if (matches.Count > 0)
			{
				var args = Enumerable.Range(0, matches.Count).Cast<object>().ToArray();
				_ = string.Format(CultureInfo.InvariantCulture, value, args);
			}

			return true;
		}
		catch (FormatException)
		{
			return false;
		}
	}
}
