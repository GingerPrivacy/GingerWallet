using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text.Json;
using System.Text.RegularExpressions;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.Lang;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Localization;

[Collection("Serial unit tests collection")]
public class LocalizationTests
{
	private readonly Regex _placeholderRegex = new(@"{\d+}", RegexOptions.Compiled);

	[Theory]
	[InlineData("de-DE", 0, "", "Bestätigt (Bestätigungen: 0)", "Bestätigungen: 0")]
	[InlineData("de-DE", 1, "1 d, 1 h, 1 min und 1 s", "Bestätigt (Bestätigungen: 1)", "Bestätigungen: 1")]
	[InlineData("de-DE", 2, "2 d, 2 h, 2 min und 2 s", "Bestätigt (Bestätigungen: 2)", "Bestätigungen: 2")]
	[InlineData("de-DE", 5, "5 d, 5 h, 5 min und 5 s", "Bestätigt (Bestätigungen: 5)", "Bestätigungen: 5")]
	[InlineData("es-ES", 0, "", "Confirmado (confirmaciones: 0)", "Confirmaciones: 0")]
	[InlineData("es-ES", 1, "1 d, 1 h, 1 min y 1 s", "Confirmado (confirmaciones: 1)", "Confirmaciones: 1")]
	[InlineData("es-ES", 2, "2 d, 2 h, 2 min y 2 s", "Confirmado (confirmaciones: 2)", "Confirmaciones: 2")]
	[InlineData("es-ES", 5, "5 d, 5 h, 5 min y 5 s", "Confirmado (confirmaciones: 5)", "Confirmaciones: 5")]
	[InlineData("fr-FR", 0, "", "Confirmé (confirmations : 0)", "Confirmations : 0")]
	[InlineData("fr-FR", 1, "1 j, 1 h, 1 min et 1 s", "Confirmé (confirmations : 1)", "Confirmations : 1")]
	[InlineData("fr-FR", 2, "2 j, 2 h, 2 min et 2 s", "Confirmé (confirmations : 2)", "Confirmations : 2")]
	[InlineData("fr-FR", 5, "5 j, 5 h, 5 min et 5 s", "Confirmé (confirmations : 5)", "Confirmations : 5")]
	[InlineData("pt-BR", 0, "", "Confirmado (confirmações: 0)", "Confirmações: 0")]
	[InlineData("pt-BR", 1, "1 d, 1 h, 1 min e 1 s", "Confirmado (confirmações: 1)", "Confirmações: 1")]
	[InlineData("pt-BR", 2, "2 d, 2 h, 2 min e 2 s", "Confirmado (confirmações: 2)", "Confirmações: 2")]
	[InlineData("pt-BR", 5, "5 d, 5 h, 5 min e 5 s", "Confirmado (confirmações: 5)", "Confirmações: 5")]
	[InlineData("ru-RU", 0, "", "Подтверждено (подтверждений: 0)", "Подтверждений: 0")]
	[InlineData("ru-RU", 1, "1 дн., 1 ч., 1 мин. и 1 с.", "Подтверждено (подтверждений: 1)", "Подтверждений: 1")]
	[InlineData("ru-RU", 2, "2 дн., 2 ч., 2 мин. и 2 с.", "Подтверждено (подтверждений: 2)", "Подтверждений: 2")]
	[InlineData("ru-RU", 5, "5 дн., 5 ч., 5 мин. и 5 с.", "Подтверждено (подтверждений: 5)", "Подтверждений: 5")]
	[InlineData("en-US", 0, "", "Confirmed (0 confirmations)", "0 confirmations")]
	[InlineData("en-US", 2, "2 days, 2 hours, 2 minutes and 2 seconds", "Confirmed (2 confirmations)", "2 confirmations")]
	public void LocalizedCountsRenderCorrectly(string cultureName, int count, string expectedDuration, string expectedStatus, string expectedCount)
	{
		var previousCulture = Resources.Culture;
		try
		{
			Resources.Culture = CultureInfo.GetCultureInfo(cultureName);
			Assert.Equal(expectedDuration, TextHelpers.TimeSpanToFriendlyString(new TimeSpan(count, count, count, count)));
			Assert.Equal(expectedStatus, TextHelpers.GetConfirmationText(count));
			Assert.Equal(expectedCount, Resources.ConfirmationCount.SafeInject(count, TextHelpers.AddSIfPlural(count)));
		}
		finally
		{
			Resources.Culture = previousCulture;
		}
	}

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
