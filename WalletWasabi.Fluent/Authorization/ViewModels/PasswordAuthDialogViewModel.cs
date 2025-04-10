using System.Threading.Tasks;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.Authorization.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class PasswordAuthDialogViewModel : AuthorizationDialogBase
{
	private readonly IWalletModel _wallet;
	[AutoNotify] private string _password;

	public PasswordAuthDialogViewModel(IWalletModel wallet, string? continueText = null)
	{
		continueText ??= Resources.Continue;

		if (wallet.IsHardwareWallet)
		{
			throw new InvalidOperationException("Passphrase authorization is not possible on hardware wallets.");
		}

		Title = Resources.EnterPassphraseAuth;

		ContinueText = continueText;

		_wallet = wallet;
		_password = "";

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		AuthorizationFailedMessage = Resources.IncorrectPassphrase.SafeInject(Environment.NewLine);
	}

	public string ContinueText { get; init; }

	protected override async Task<bool> AuthorizeAsync()
	{
		var success = await _wallet.Auth.TryPasswordAsync(Password);
		Password = "";
		return success;
	}
}
