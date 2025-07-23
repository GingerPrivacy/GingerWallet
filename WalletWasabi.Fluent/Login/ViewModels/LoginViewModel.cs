using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.AddWallet.ViewModels;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Login.ViewModels;

public partial class LoginViewModel : RoutableViewModel
{
	[AutoNotify] private string _password;
	[AutoNotify] private bool _isPasswordNeeded;
	[AutoNotify] private string _errorMessage;

	public LoginViewModel(WalletModel wallet)
	{
		_password = "";
		_errorMessage = "";
		IsPasswordNeeded = !wallet.IsWatchOnlyWallet;
		WalletName = wallet.Name;
		WalletType = wallet.Settings.WalletType;

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(wallet));

		OkCommand = ReactiveCommand.Create(OnOk);

		EnableAutoBusyOn(NextCommand);
	}

	public WalletType WalletType { get; }

	public string WalletName { get; }

	public ICommand OkCommand { get; }

	private async Task OnNextAsync(WalletModel walletModel)
	{
		var (success, compatibilityPasswordUsed) = await walletModel.Auth.TryLoginAsync(Password);

		if (!success)
		{
			ErrorMessage = Resources.IncorrectPassphraseRetry;
			return;
		}

		if (compatibilityPasswordUsed)
		{
			await ShowErrorAsync(Title, PasswordHelper.CompatibilityPasswordWarnMessage, "");
		}

		var termsAndConditionsAccepted = await TermsAndConditionsViewModel.TryShowAsync(UiContext, walletModel);
		if (termsAndConditionsAccepted)
		{
			await walletModel.Loader.WaitingForBackgroundServicesAsync();
			walletModel.Auth.CompleteLogin();
		}
		else
		{
			walletModel.Auth.Logout();
			ErrorMessage = Resources.AcceptTermsAndConditions;
		}
	}

	private void OnOk()
	{
		Password = "";
		ErrorMessage = "";
	}
}
