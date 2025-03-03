using WalletWasabi.Fluent.Navigation.ViewModels;

namespace WalletWasabi.Fluent.Common.ViewModels.DialogBase;

/// <summary>
/// CommonBase class.
/// </summary>
public abstract partial class DialogViewModelBase : RoutableViewModel
{
	[AutoNotify] private bool _isDialogOpen;
}
