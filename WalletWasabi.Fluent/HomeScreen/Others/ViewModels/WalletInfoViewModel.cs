using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.Others.ViewModels;

[NavigationMetaData(
	IconName = "nav_wallet_24_regular",
	Order = 4,
	Category = SearchCategory.Wallet,
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false,
	IsLocalized = true)]
public partial class WalletInfoViewModel : RoutableViewModel
{
	private readonly WalletInfoModel _model;

	[AutoNotify] private bool _showSensitiveData;
	[AutoNotify] private string _showButtonText = Resources.ShowSensitiveData;
	[AutoNotify] private string _lockIconString = "eye_show_regular";

	public WalletInfoViewModel(WalletModel wallet)
	{
		_model = wallet.GetWalletInfo();
		IsHardwareWallet = wallet.IsHardwareWallet;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableCancel = !wallet.IsWatchOnlyWallet;

		NextCommand = ReactiveCommand.Create(() => UiContext.Navigate(CurrentTarget).Clear());

		CancelCommand = ReactiveCommand.Create(() =>
		{
			ShowSensitiveData = !ShowSensitiveData;
			ShowButtonText = ShowSensitiveData ? Resources.HideSensitiveData : Resources.ShowSensitiveData;
			LockIconString = ShowSensitiveData ? "eye_hide_regular" : "eye_show_regular";
		});
	}

	public string SegWitExtendedAccountPublicKey => _model.SegWitExtendedAccountPublicKey;

	public string? TaprootExtendedAccountPublicKey => _model.TaprootExtendedAccountPublicKey;

	public string SegWitAccountKeyPath => _model.SegWitAccountKeyPath;

	public string TaprootAccountKeyPath => _model.TaprootAccountKeyPath;

	public string? MasterKeyFingerprint => _model.MasterKeyFingerprint;

	public string? ExtendedMasterPrivateKey => _model.ExtendedMasterPrivateKey;

	public string? ExtendedAccountPrivateKey => _model.ExtendedAccountPrivateKey;

	public string? ExtendedMasterZprv => _model.ExtendedMasterZprv;

	public bool HasOutputDescriptors => _model.WpkhOutputDescriptors is not null;

	public string? PublicExternalOutputDescriptor => _model.WpkhOutputDescriptors?.PublicExternal.ToString();

	public string? PublicInternalOutputDescriptor => _model.WpkhOutputDescriptors?.PublicInternal.ToString();

	public string? PrivateExternalOutputDescriptor => _model.WpkhOutputDescriptors?.PrivateExternal;

	public string? PrivateInternalOutputDescriptor => _model.WpkhOutputDescriptors?.PrivateInternal;

	public bool IsHardwareWallet { get; }
}
