using System.ComponentModel;
using WalletWasabi.Models;

namespace WalletWasabi.Lang.Models;

public enum GroupSeparator
{
	[FriendlyName(isLocalized: true)]
	[Char("\0")]
	None,

	[FriendlyName(isLocalized: true)]
	[Char(" ")]
	Space,

	[FriendlyName(isLocalized: true)]
	[Char(".")]
	Dot,

	[FriendlyName(isLocalized: true)]
	[Char(",")]
	Comma,

	[FriendlyName(isLocalized: true)]
	[Char("’")]
	Apostrophe,
}
