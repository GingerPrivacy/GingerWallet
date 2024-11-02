using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Settings;

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
