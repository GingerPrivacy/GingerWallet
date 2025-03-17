using System.Globalization;
using System.Resources;
using WalletWasabi.Extensions;
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
			_ => language.ToString()
		};
	}

	public static string[] ToKeywords(this string words)
	{
		return words.Replace(" ", "").Split(',');
	}
}
