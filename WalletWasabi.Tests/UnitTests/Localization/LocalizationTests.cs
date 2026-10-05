using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text.Json;
using System.Text.RegularExpressions;
using WalletWasabi.Extensions;
using WalletWasabi.JsonConverters;
using WalletWasabi.Lang;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Localization;

public class LocalizationTests
{
	private readonly Regex _placeholderRegex = new(@"{\d+}", RegexOptions.Compiled);

	[Theory]
	[InlineData(DisplayLanguage.German, "de-DE", "de", "Deutsch", "Sprache")]
	[InlineData(DisplayLanguage.Spanish, "es-ES", "es", "Español", "Idioma")]
	[InlineData(DisplayLanguage.French, "fr-FR", "fr", "Français", "Langue")]
	[InlineData(DisplayLanguage.BrazilianPortuguese, "pt-BR", "pt-BR", "Português (Brasil)", "Idioma")]
	[InlineData(DisplayLanguage.Russian, "ru-RU", "ru", "Русский", "Язык")]
	public void RequestedLanguagesHaveCompleteTranslations(DisplayLanguage language, string cultureName, string resourceCultureName, string nativeName, string languageLabel)
	{
		Assert.Equal(cultureName, language.GetDescription());
		Assert.Equal(nativeName, language.ToLocalTranslation());
		Assert.Equal(languageLabel, Resources.ResourceManager.GetString(nameof(Resources.Language), CultureInfo.GetCultureInfo(cultureName)));

		// Disable parent fallback so a missing translation cannot pass using English resources.
		var translations = Resources.ResourceManager.GetResourceSet(CultureInfo.GetCultureInfo(resourceCultureName), true, false);
		var english = Resources.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, false);
		Assert.NotNull(translations);
		Assert.NotNull(english);
		Assert.Equal(english.Cast<DictionaryEntry>().Select(x => x.Key).OrderBy(x => x), translations.Cast<DictionaryEntry>().Select(x => x.Key).OrderBy(x => x));

		foreach (DictionaryEntry entry in english)
		{
			var key = (string)entry.Key;
			var translation = translations.GetString(key);
			Assert.False(string.IsNullOrWhiteSpace(translation), $"Missing translation for '{key}' in {cultureName}.");
			Assert.Equal(GetFormatItems((string)entry.Value!), GetFormatItems(translation!));
		}
	}

	[Theory]
	[InlineData(10, DisplayLanguage.BrazilianPortuguese)]
	[InlineData(11, DisplayLanguage.Russian)]
	public void NewLanguageSettingsSurviveSerialization(int savedValue, DisplayLanguage language)
	{
		var options = new JsonSerializerOptions();
		options.Converters.Add(new DisplayLanguageJsonConverter());
		var restoredValue = JsonSerializer.Deserialize<int>(savedValue.ToString(CultureInfo.InvariantCulture), options);
		Assert.Equal(language, (DisplayLanguage)restoredValue);
		Assert.Equal(savedValue.ToString(CultureInfo.InvariantCulture), JsonSerializer.Serialize(restoredValue, options));
	}

	private static IEnumerable<string> GetFormatItems(string value)
	{
		return Regex.Matches(value, @"(?<!\{)\{\d+(?:,[^}:]+)?(?::[^}]+)?\}(?!\})").Select(x => x.Value).OrderBy(x => x);
	}

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
	public void ConsistentSentenceClosingTest()
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

		var specialEndings = new HashSet<char>
		{
			'.', ',', '!', '?', ':', ';',
			'。', '，', '！', '？', '：', '；'
		};

		foreach (var (key, values) in allValue)
		{
			char? baseEndingChar = null;

			foreach (string value in values)
			{
				char lastChar = value[^1];

				if (!specialEndings.Contains(lastChar))
				{
					continue;
				}

				if (baseEndingChar is not { } baseChar)
				{
					baseEndingChar = lastChar;
					continue;
				}

				Assert.True(
					baseChar == lastChar || CompareChinese(baseChar, lastChar) || CompareChinese(lastChar, baseChar),
					$"Mismatched sentence ending character for '{key}' in value: '{value}'");
			}
		}

		return;

		bool CompareChinese(char first, char second)
		{
			var equivalents = new Dictionary<char, char>
			{
				{ '.', '。' },
				{ ',', '，' },
				{ '!', '！' },
				{ '?', '？' },
				{ ':', '：' },
				{ ';', '；' }
			};

			return equivalents.TryGetValue(first, out var chineseEquivalent) && chineseEquivalent == second;
		}
	}

	[Fact]
	public void EnsureSingleSpacingTest()
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
					Assert.False(HasConsecutiveInnerWhitespaces(value), $"Invalid spacing in key '{entry.Key}' for culture: {lang}");
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

	public static bool HasConsecutiveInnerWhitespaces(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return false;
		}

		var trimmed = input.Trim();

		for (var i = 1; i < trimmed.Length; i++)
		{
			if (char.IsWhiteSpace(trimmed[i]) && char.IsWhiteSpace(trimmed[i - 1]))
			{
				return true;
			}
		}

		return false;
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
