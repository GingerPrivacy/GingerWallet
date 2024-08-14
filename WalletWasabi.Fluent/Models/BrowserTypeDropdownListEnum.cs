using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models;

public enum BrowserTypeDropdownListEnum
{
	[FriendlyName("System Default")]
	SystemDefault,

	Tor,
	Brave,
	Firefox,
	Chrome,
	Opera,

	[FriendlyName("Internet Explorer")]
	InternetExplorer,

	Safari,
	Custom
}
