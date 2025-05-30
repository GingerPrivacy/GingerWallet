using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Lang;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.TwoFactor.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class VerifyTwoFactoryAuthenticationDialogViewModel : DialogViewModelBase<bool>
{
	[AutoNotify] private string _twoFactorToken = "";

	public VerifyTwoFactoryAuthenticationDialogViewModel()
	{
		Title = Resources.TwoFactorAuthenticationWithCapitals;

		NextCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			try
			{
				IsBusy = true;

				await UiContext.TwoFactorAuthentication.LoginVerifyAsync(TwoFactorToken).ConfigureAwait(false);
				UiContext.WalletRepository.LoadWalletListFromFileSystem();

				Close(result: true);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, Resources.TokenVerificationFailed, "");
			}
			finally
			{
				IsBusy = false;
			}
		}, this.WhenAnyValue<VerifyTwoFactoryAuthenticationDialogViewModel, string>(x => x.TwoFactorToken).Select(x => !string.IsNullOrEmpty(x) && x.Length == 8));

		this.WhenAnyValue<VerifyTwoFactoryAuthenticationDialogViewModel, string>(x => x.TwoFactorToken)
			.Where(x => !string.IsNullOrEmpty(x) && x.Length == 8)
			.Take(1)
			.Do(_ => NextCommand.ExecuteIfCan())
			.Subscribe();

		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);
	}
}
