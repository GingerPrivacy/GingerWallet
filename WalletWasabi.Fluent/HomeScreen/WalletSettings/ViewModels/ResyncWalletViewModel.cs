using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.WalletSettings.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ResyncWalletViewModel : DialogViewModelBase<bool>
{
	public ResyncWalletViewModel()
	{
		Title = Resources.ResyncWallet;
		SetupCancel(false, true, true);
		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, true));
	}
}
