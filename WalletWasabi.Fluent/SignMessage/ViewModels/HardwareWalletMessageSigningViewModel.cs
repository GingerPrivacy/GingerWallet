using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.SignMessage.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class HardwareWalletMessageSigningViewModel : DialogViewModelBase<string?>
{
	[AutoNotify] private string _authorizationFailedMessage = "";

	public HardwareWalletMessageSigningViewModel(HardwareWalletModel wallet, string message, HdPubKey hdPubKey)
	{
		WalletType = wallet.Settings.WalletType;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = false;

		NextCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			AuthorizationFailedMessage = "";

			try
			{
				var signature = await wallet.SignMessageAsync(message, hdPubKey);
				Close(result: signature);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				AuthorizationFailedMessage = ex.ToUserFriendlyString();
			}
		});

		EnableAutoBusyOn(NextCommand);
	}

	public WalletType WalletType { get; }
}
