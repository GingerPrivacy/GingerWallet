using WalletWasabi.Fluent.Models;
using WalletWasabi.Lang;

// Namespace does not match folder structure (see https://github.com/zkSNACKs/WalletWasabi/pull/10576#issuecomment-1552750543)
#pragma warning disable IDE0130

namespace WalletWasabi.Fluent;

public sealed record NavigationMetaData(
	bool Searchable = true,
	bool IsLocalized = false,
	string? Title = null,
	string? Caption = null,
	string? IconName = null,
	string? IconNameFocused = null,
	int Order = 0,
	SearchCategory Category = SearchCategory.None,
	string? Keywords = null,
	NavBarPosition NavBarPosition = default,
	NavBarSelectionMode NavBarSelectionMode = default,
	NavigationTarget NavigationTarget = default
)
{
	public string[]? GetKeywords() => Keywords?.ToKeywords();

	public string GetCategoryString()
	{
		return Category switch
		{
			SearchCategory.None => Lang.Resources.NoCategory,
			SearchCategory.General => Lang.Resources.General,
			SearchCategory.Wallet => Lang.Resources.Wallet,
			SearchCategory.HelpAndSupport => Lang.Resources.HelpAndSupport,
			SearchCategory.Open => Lang.Resources.Open,
			SearchCategory.Settings => Lang.Resources.Settings,
			_ => ""
		};
	}
};
