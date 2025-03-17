using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Settings.Models;

public record LocalizationPreset(DisplayLanguage Language, string ExchangeCurrency)
{
	public static LocalizationPreset GetPreset(DisplayLanguage lang)
	{
		return lang switch
		{
			DisplayLanguage.English => new LocalizationPreset(DisplayLanguage.English, "USD"),
			DisplayLanguage.Spanish => new LocalizationPreset(DisplayLanguage.Spanish, "EUR"),
			DisplayLanguage.Hungarian => new LocalizationPreset(DisplayLanguage.Hungarian, "HUF"),
			DisplayLanguage.French => new LocalizationPreset(DisplayLanguage.French, "EUR"),
			DisplayLanguage.Chinese => new LocalizationPreset(DisplayLanguage.Chinese, "CNY"),
			_ => new LocalizationPreset(lang, "USD")
		};
	}
}
