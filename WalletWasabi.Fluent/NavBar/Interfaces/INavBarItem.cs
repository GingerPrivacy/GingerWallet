// Namespace does not match folder structure (see https://github.com/zkSNACKs/WalletWasabi/pull/10576#issuecomment-1552750543)

#pragma warning disable IDE0130

using System.ComponentModel;

namespace WalletWasabi.Fluent;

public interface INavBarItem : INotifyPropertyChanged
{
	string Title { get; }
	string IconName { get; }
	string IconNameFocused { get; }
}
