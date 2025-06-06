using System.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.ClientConfig;
using WalletWasabi.Fluent.Models.FileSystem;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.SearchBar.ViewModels.Sources;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent;

public class App : Application
{
	private readonly bool _startInBg;
	private readonly Func<Task>? _backendInitialiseAsync;
	private ApplicationStateManager? _applicationStateManager;

	public App()
	{
		Name = "Ginger Wallet";
	}

	public App(Func<Task> backendInitialiseAsync, bool startInBg) : this()
	{
		_startInBg = startInBg;
		_backendInitialiseAsync = backendInitialiseAsync;
	}

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (!Design.IsDesignMode)
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				var uiContext = CreateUiContext();
				UiContext.Default = uiContext;
				_applicationStateManager =
					new ApplicationStateManager(desktop, uiContext, _startInBg);

				DataContext = _applicationStateManager.ApplicationViewModel;

				desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
				desktop.Exit += (sender, args) =>
				{
					MainViewModel.Instance.ClearStacks();
					uiContext.HealthMonitor.Dispose();
				};

				RxApp.MainThreadScheduler.Schedule(
					async () =>
					{
						await _backendInitialiseAsync!(); // Guaranteed not to be null when not in designer.

						MainViewModel.Instance.Initialize();
					});

				InitializeTrayIcons();
			}
		}

		base.OnFrameworkInitializationCompleted();
#if DEBUG
		this.AttachDevTools();
#endif
	}

	private void InitializeTrayIcons()
	{
		// TODO: This is temporary workaround until https://github.com/zkSNACKs/WalletWasabi/issues/8151 is fixed.
		var trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault();
		if (trayIcon is not null)
		{
			if (this.TryFindResource("DefaultNativeMenu", out var nativeMenu))
			{
				trayIcon.Menu = nativeMenu as NativeMenu;
			}
		}
	}

	// It begins to show that we're re-inventing DI, aren't we?
	private static WalletRepository CreateWalletRepository(AmountProvider amountProvider, HardwareWalletInterface hardwareWalletInterface)
	{
		return new WalletRepository(amountProvider, hardwareWalletInterface);
	}

	private static HardwareWalletInterface CreateHardwareWalletInterface(Network network)
	{
		return new HardwareWalletInterface(network);
	}

	private static FileSystemModel CreateFileSystem()
	{
		return new FileSystemModel();
	}

	private static ClientConfigModel CreateConfig()
	{
		return new ClientConfigModel();
	}

	private static ApplicationSettings CreateApplicationSettings(TwoFactorAuthentication twoFactorAuthentication)
	{
		return new ApplicationSettings(Services.PersistentConfigFilePath, Services.PersistentConfig, Services.Config, Services.UiConfig, twoFactorAuthentication);
	}

	private static TransactionBroadcasterModel CreateBroadcaster(Network network)
	{
		return new TransactionBroadcasterModel(network);
	}

	private static AmountProvider CreateAmountProvider()
	{
		return new AmountProvider(Services.HostedServices.Get<ExchangeRateService>());
	}

	private UiContext CreateUiContext()
	{
		var twoFactorAuthentication = new TwoFactorAuthentication();
		var applicationSettings = CreateApplicationSettings(twoFactorAuthentication);
		var amountProvider = CreateAmountProvider();
		var torStatusChecker = new TorStatusCheckerModel();
		var hardwareWalletInterface = CreateHardwareWalletInterface(applicationSettings.Network);

		// This class (App) represents the actual Avalonia Application and it's sole presence means we're in the actual runtime context (as opposed to unit tests)
		// Once all ViewModels have been refactored to receive UiContext as a constructor parameter, this static singleton property can be removed.
		return new UiContext(
			new QrCodeGenerator(),
			new QrCodeReader(),
			new UiClipboard(),
			CreateWalletRepository(amountProvider, hardwareWalletInterface),
			new CoinjoinModel(),
			hardwareWalletInterface,
			CreateFileSystem(),
			CreateConfig(),
			applicationSettings,
			CreateBroadcaster(applicationSettings.Network),
			amountProvider,
			new EditableSearchSourceSource(),
			torStatusChecker,
			new LegalDocumentsProvider(),
			new HealthMonitor(applicationSettings, torStatusChecker),
			twoFactorAuthentication);
	}
}
