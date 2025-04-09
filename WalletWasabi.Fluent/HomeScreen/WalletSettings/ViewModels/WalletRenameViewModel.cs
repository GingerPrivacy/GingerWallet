using System.Reactive;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.WalletSettings.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class WalletRenameViewModel : DialogViewModelBase<Unit>
{
	[AutoNotify] private string _newWalletName;

	public WalletRenameViewModel(IWalletModel wallet)
	{
		Title = Resources.RenameWallet;

		_newWalletName = wallet.Name;

		this.ValidateProperty(
			x => x.NewWalletName,
			errors =>
			{
				if (wallet.Name == NewWalletName)
				{
					return;
				}

				if (UiContext.WalletRepository.ValidateWalletName(NewWalletName) is { } error)
				{
					errors.Add(error.Severity, error.Message);
				}
			});

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		var canRename = this.WhenAnyValue(model => model.NewWalletName, selector: _ => !Validations.Any);
		NextCommand = ReactiveCommand.Create(() => OnRename(wallet), canRename);
	}

	private void OnRename(IWalletModel wallet)
	{
		try
		{
			wallet.Rename(NewWalletName);
			Navigate().Back();
		}
		catch
		{
			UiContext.Navigate().To().ShowErrorDialog(Resources.WalletCannotBeRenamed.SafeInject(NewWalletName), Resources.InvalidWalletName, "", NavigationTarget.CompactDialogScreen);
		}
	}
}
