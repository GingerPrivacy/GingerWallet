using System.ComponentModel;
using WalletWasabi.Models;

namespace WalletWasabi.Lang.Models;

public enum DecimalSeparator
{
	[FriendlyName(isLocalized: true)]
	[Char(".")]
	Dot,

	[FriendlyName(isLocalized: true)]
	[Char(",")]
	Comma,
}
