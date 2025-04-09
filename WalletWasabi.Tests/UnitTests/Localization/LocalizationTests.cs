using System.Collections;
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
