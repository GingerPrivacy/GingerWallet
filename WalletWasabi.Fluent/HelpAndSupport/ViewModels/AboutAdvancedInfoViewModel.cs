using System.Globalization;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Helpers;
using WalletWasabi.Lang;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent.HelpAndSupport.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class AboutAdvancedInfoViewModel : DialogViewModelBase<System.Reactive.Unit>
{
	public AboutAdvancedInfoViewModel()
	{
		Title = Resources.AdvancedInformation;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		NextCommand = CancelCommand;
	}

	public Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;

	public Version HwiVersion => Constants.HwiVersion;

	public string BackendCompatibleVersions => Constants.ClientSupportBackendVersionText;

	public string CurrentBackendMajorVersion => WasabiClient.ApiVersion.ToString(CultureInfo.InvariantCulture);

	protected override void OnDialogClosed()
	{
	}
}
