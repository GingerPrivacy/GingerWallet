using System.Globalization;
using System.Resources;

namespace WalletWasabi.Lang;

public static class ResourceManagerExtension
{
	public static string GetSafeValue(this ResourceManager manager, string key)
	{
		var culture = Resources.Culture;

		// TODO: Shouldn't we rather set this?
		// var culture = CultureInfo.CurrentUICulture;

		var result = manager.GetString(key, culture) ?? "";

		return result;
	}
}
