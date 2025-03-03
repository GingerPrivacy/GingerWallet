using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Helpers;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent.HelpAndSupport.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class AboutAdvancedInfoViewModel : DialogViewModelBase<System.Reactive.Unit>
{
	public AboutAdvancedInfoViewModel()
	{
		Title = "About";

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		NextCommand = CancelCommand;
	}

	public Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;

	public Version HwiVersion => Constants.HwiVersion;

	public string BackendCompatibleVersions => Constants.ClientSupportBackendVersionText;

	public string CurrentBackendMajorVersion => WasabiClient.ApiVersion.ToString();

	protected override void OnDialogClosed()
	{
	}
}
