using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models;

public enum BrowserTypeDropdownListEnum
{
	[FriendlyName(isLocalized: true)] SystemDefault,

	Tor,
	Brave,
	Firefox,
	Chrome,
	Opera,

	[FriendlyName("Internet Explorer")] InternetExplorer,

	Safari,

	[FriendlyName(isLocalized: true)] Custom
}
