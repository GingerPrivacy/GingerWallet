using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;

namespace WalletWasabi.Fluent.Common.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class ShowErrorDialogViewModel : DialogViewModelBase<bool>
{
	public ShowErrorDialogViewModel(string message, string title, string caption)
	{
		Message = message;
		Title = title;
		Caption = caption;

		NextCommand = ReactiveCommand.Create(() => Close());

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public string Message { get; }
}
