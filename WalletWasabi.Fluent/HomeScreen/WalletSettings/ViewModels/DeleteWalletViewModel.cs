using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.WalletSettings.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class DeleteWalletViewModel : DialogViewModelBase<bool>
{
	[AutoNotify] private string? _input;

	public DeleteWalletViewModel(string walletName)
	{
		Title = Resources.DeleteWallet;
		WalletName = walletName;
		SetupCancel(true, true, true);

		var canExecute = this.WhenAnyValue(x => x.Input).Select(x => x == walletName);
		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, true), canExecute);
	}

	public string WalletName { get; }
}
