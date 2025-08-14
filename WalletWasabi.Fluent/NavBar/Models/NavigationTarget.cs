// Namespace does not match folder structure (see https://github.com/zkSNACKs/WalletWasabi/pull/10576#issuecomment-1552750543)

#pragma warning disable IDE0130

namespace WalletWasabi.Fluent;

public enum NavigationTarget
{
	Unspecified = 0,
	HomeScreen = 1,
	DialogScreen = 2,
	CompactDialogScreen = 4,
}
