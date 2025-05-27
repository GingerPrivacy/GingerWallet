using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models.ClientConfig;
using WalletWasabi.Fluent.Models.FileSystem;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.Interfaces;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Fluent.SearchBar.ViewModels.Sources;

namespace WalletWasabi.Fluent.Models.UI;

public class UiContext
{
	/// <summary>
	///     The use of this property is a temporary workaround until we finalize the refactoring of all ViewModels (to be
	///     testable)
	/// </summary>
	public static UiContext Default = null!;

	private NavigationState? _navigate;

	public UiContext(
		QrCodeGenerator qrCodeGenerator,
		QrCodeReader qrCodeReader,
		UiClipboard clipboard,
		WalletRepository walletRepository,
		CoinjoinModel coinJoinModel,
		HardwareWalletInterface hardwareWalletInterface,
		FileSystemModel fileSystem,
		ClientConfigModel config,
		ApplicationSettings applicationSettings,
		TransactionBroadcasterModel transactionBroadcaster,
		AmountProvider amountProvider,
		EditableSearchSourceSource editableSearchSource,
		TorStatusCheckerModel torStatusChecker,
		LegalDocumentsProvider legalDocumentsProvider,
		HealthMonitor healthMonitor,
		TwoFactorAuthentication twoFactorAuthentication)
	{
		QrCodeGenerator = qrCodeGenerator ?? throw new ArgumentNullException(nameof(qrCodeGenerator));
		QrCodeReader = qrCodeReader ?? throw new ArgumentNullException(nameof(qrCodeReader));
		Clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
		WalletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
		CoinjoinModel = coinJoinModel ?? throw new ArgumentNullException(nameof(coinJoinModel));
		HardwareWalletInterface = hardwareWalletInterface ?? throw new ArgumentNullException(nameof(hardwareWalletInterface));
		FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
		Config = config ?? throw new ArgumentNullException(nameof(config));
		ApplicationSettings = applicationSettings ?? throw new ArgumentNullException(nameof(applicationSettings));
		TransactionBroadcaster = transactionBroadcaster ?? throw new ArgumentNullException(nameof(transactionBroadcaster));
		AmountProvider = amountProvider ?? throw new ArgumentNullException(nameof(amountProvider));
		EditableSearchSource = editableSearchSource ?? throw new ArgumentNullException(nameof(editableSearchSource));
		TorStatusChecker = torStatusChecker ?? throw new ArgumentNullException(nameof(torStatusChecker));
		LegalDocumentsProvider = legalDocumentsProvider ?? throw new ArgumentNullException(nameof(legalDocumentsProvider));
		HealthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
		TwoFactorAuthentication = twoFactorAuthentication ?? throw new ArgumentNullException(nameof(twoFactorAuthentication));

		if (Default != null)
		{
			throw new InvalidOperationException($"MainViewModel instantiated more than once.");
		}

		Default = this;
	}

	public UiClipboard Clipboard { get; }
	public QrCodeGenerator QrCodeGenerator { get; }
	public WalletRepository WalletRepository { get; }
	public CoinjoinModel CoinjoinModel { get; }
	public QrCodeReader QrCodeReader { get; }
	public HardwareWalletInterface HardwareWalletInterface { get; }
	public FileSystemModel FileSystem { get; }
	public ClientConfigModel Config { get; }
	public ApplicationSettings ApplicationSettings { get; }
	public TransactionBroadcasterModel TransactionBroadcaster { get; }
	public AmountProvider AmountProvider { get; }
	public EditableSearchSourceSource EditableSearchSource { get; }
	public TorStatusCheckerModel TorStatusChecker { get; }
	public LegalDocumentsProvider LegalDocumentsProvider { get; }
	public HealthMonitor HealthMonitor { get; }
	public TwoFactorAuthentication TwoFactorAuthentication { get; }
	public MainViewModel? MainViewModel { get; private set; }

	public void RegisterNavigation(NavigationState navigate)
	{
		_navigate ??= navigate;
	}

	public NavigationState Navigate()
	{
		return _navigate ?? throw new InvalidOperationException($"{GetType().Name} {nameof(_navigate)} hasn't been initialized.");
	}

	public INavigationStack<RoutableViewModel> Navigate(NavigationTarget target)
	{
		return
			_navigate?.Navigate(target)
			?? throw new InvalidOperationException($"{GetType().Name} {nameof(_navigate)} hasn't been initialized.");
	}

	public void SetMainViewModel(MainViewModel viewModel)
	{
		MainViewModel ??= viewModel;
	}
}
