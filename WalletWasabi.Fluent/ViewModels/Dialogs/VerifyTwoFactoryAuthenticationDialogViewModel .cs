using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Two-Factor Authentication", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class VerifyTwoFactoryAuthenticationDialogViewModel : DialogViewModelBase<bool>
{
	[AutoNotify] private string _twoFactorToken = "";

	private VerifyTwoFactoryAuthenticationDialogViewModel()
	{
		NextCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			try
			{
				IsBusy = true;

				await UiContext.TwoFactorAuthenticationModel.LoginVerifyAsync(TwoFactorToken).ConfigureAwait(false);
				UiContext.WalletRepository.LoadWalletListFromFileSystem();

				Close(result: true);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, "Couldn't verify the token, please see the logs for further information.", "");
			}
			finally
			{
				IsBusy = false;
			}
		}, this.WhenAnyValue(x => x.TwoFactorToken).Select(x => !string.IsNullOrEmpty(x) && x.Length == 8));

		this.WhenAnyValue(x => x.TwoFactorToken)
			.Where(x => !string.IsNullOrEmpty(x) && x.Length == 8)
			.Take(1)
			.Do(_ => NextCommand.ExecuteIfCan())
			.Subscribe();

		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);
	}
}
