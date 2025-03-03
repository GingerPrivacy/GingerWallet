using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Controls;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Bases;
using WalletWasabi.Daemon;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.HomeScreen.BuySell.Models;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;
using Unit = System.Reactive.Unit;

namespace WalletWasabi.Fluent.Models.UI;

[AppLifetime]
[AutoInterface]
public partial class ApplicationSettings : ReactiveObject
{
	private const int ThrottleTime = 500;

	private readonly Subject<bool> _isRestartNeeded = new();
	private readonly string _persistentConfigFilePath;
	private readonly PersistentConfig _startupConfig;
	private readonly Config _config;
	private readonly UiConfig _uiConfig;
	private readonly ITwoFactorAuthentication _twoFactorAuthentication;

	// Advanced
	[AutoNotify] private bool _enableGpu;

	// Bitcoin
	[AutoNotify] private Network _network;

	[AutoNotify] private bool _startLocalBitcoinCoreOnStartup;
	[AutoNotify] private string _localBitcoinCoreDataDir;
	[AutoNotify] private bool _stopLocalBitcoinCoreOnShutdown;
	[AutoNotify] private string _bitcoinP2PEndPoint;
	[AutoNotify] private string _coordinatorUri;
	[AutoNotify] private string _dustThreshold;

	// General
	[AutoNotify] private bool _darkModeEnabled;

	[AutoNotify] private bool _autoCopy;
	[AutoNotify] private bool _autoPaste;
	[AutoNotify] private bool _customChangeAddress;
	[AutoNotify] private FeeDisplayUnit _selectedFeeDisplayUnit;
	[AutoNotify] private bool _runOnSystemStartup;
	[AutoNotify] private bool _hideOnClose;
	[AutoNotify] private TorMode _useTor;
	[AutoNotify] private bool _terminateTorOnExit;
	[AutoNotify] private bool _downloadNewVersion;
	[AutoNotify] private BrowserTypeDropdownListEnum _selectedBrowser;
	[AutoNotify] private string _browserPath;
	[AutoNotify] private DisplayLanguage _selectedDisplayLanguage;

	// Privacy Mode
	[AutoNotify] private bool _privacyMode;

	[AutoNotify] private bool _oobe;
	[AutoNotify] private WindowState _windowState;

	// Non-persistent
	[AutoNotify] private bool _doUpdateOnClose;

	//Buy Sell
	[AutoNotify] private BuySellConfiguration _buySellConfiguration;


	public ApplicationSettings(string persistentConfigFilePath, PersistentConfig persistentConfig, Config config, UiConfig uiConfig, ITwoFactorAuthentication twoFactorAuthentication)
	{
		_persistentConfigFilePath = persistentConfigFilePath;
		_startupConfig = persistentConfig;

		_config = config;
		_uiConfig = uiConfig;
		_twoFactorAuthentication = twoFactorAuthentication;

		// Advanced
		_enableGpu = _startupConfig.EnableGpu;

		// Bitcoin
		_network = config.Network;
		_startLocalBitcoinCoreOnStartup = _startupConfig.StartLocalBitcoinCoreOnStartup;
		_localBitcoinCoreDataDir = _startupConfig.LocalBitcoinCoreDataDir;
		_stopLocalBitcoinCoreOnShutdown = _startupConfig.StopLocalBitcoinCoreOnShutdown;
		_bitcoinP2PEndPoint = _startupConfig.GetBitcoinP2pEndPoint().ToString(defaultPort: -1);
		_coordinatorUri = _startupConfig.GetCoordinatorUri();
		_dustThreshold = _startupConfig.DustThreshold.ToString();

		// General
		_darkModeEnabled = _uiConfig.DarkModeEnabled;
		_autoCopy = _uiConfig.Autocopy;
		_autoPaste = _uiConfig.AutoPaste;
		_customChangeAddress = _uiConfig.IsCustomChangeAddress;
		_selectedFeeDisplayUnit = Enum.IsDefined(typeof(FeeDisplayUnit), _uiConfig.FeeDisplayUnit)
			? (FeeDisplayUnit)_uiConfig.FeeDisplayUnit
			: FeeDisplayUnit.Satoshis;
		_browserPath = _uiConfig.SelectedBrowser;
		_selectedBrowser = GetSelectedBrowser();
		_runOnSystemStartup = _uiConfig.RunOnSystemStartup;
		_hideOnClose = _uiConfig.HideOnClose;
		_useTor = Config.ObjectToTorMode(_config.UseTor);
		_terminateTorOnExit = _startupConfig.TerminateTorOnExit;
		_downloadNewVersion = _startupConfig.DownloadNewVersion;
		_selectedDisplayLanguage = (DisplayLanguage)_startupConfig.DisplayLanguage;

		// Privacy Mode
		_privacyMode = _uiConfig.PrivacyMode;

		// Buy Sell
		_buySellConfiguration = _uiConfig.BuySellConfiguration;

		_oobe = _uiConfig.Oobe;
		_windowState = (WindowState)Enum.Parse(typeof(WindowState), _uiConfig.WindowState);


		// Save on change
		this.WhenAnyValue(
			x => x.EnableGpu,
			x => x.Network,
			x => x.StartLocalBitcoinCoreOnStartup,
			x => x.LocalBitcoinCoreDataDir,
			x => x.StopLocalBitcoinCoreOnShutdown,
			x => x.BitcoinP2PEndPoint,
			x => x.CoordinatorUri,
			x => x.DustThreshold,
			x => x.UseTor,
			x => x.TerminateTorOnExit,
			x => x.DownloadNewVersion,
			(_, _, _, _, _, _, _, _, _, _, _) => Unit.Default)
			.Skip(1)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
			.Do(_ => Save())
			.Subscribe();

		// Save UiConfig on change
		this.WhenAnyValue(
				x => x.DarkModeEnabled,
				x => x.AutoCopy,
				x => x.AutoPaste,
				x => x.CustomChangeAddress,
				x => x.SelectedFeeDisplayUnit,
				x => x.RunOnSystemStartup,
				x => x.HideOnClose,
				x => x.Oobe,
				x => x.WindowState,
				x => x.BuySellConfiguration)
			.Skip(1)
			.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
			.Do(_ => ApplyUiConfigChanges())
			.Subscribe();

		// Saving is not necessary; this call is only for evaluating if a restart is needed.
		this.WhenAnyValue(x => x._twoFactorAuthentication.TwoFactorEnabled)
			.Subscribe(_ => Save());

		// Saving is not necessary; this call is only for evaluating if a restart is needed.
		this.WhenAnyValue(x => x.SelectedDisplayLanguage)
			.Subscribe(_ => Save());

		// Save UiConfig on change without throttling
		this.WhenAnyValue(
				x => x.PrivacyMode)
			.Skip(1)
			.Do(_ => ApplyUiConfigPrivacyModeChange())
			.Subscribe();

		// Set Default BitcoinCoreDataDir if required
		this.WhenAnyValue(x => x.StartLocalBitcoinCoreOnStartup)
			.Skip(1)
			.Where(value => value && string.IsNullOrEmpty(LocalBitcoinCoreDataDir))
			.Subscribe(_ => LocalBitcoinCoreDataDir = EnvironmentHelpers.GetDefaultBitcoinCoreDataDirOrEmptyString());

		// Apply RunOnSystemStartup
		this.WhenAnyValue(x => x.RunOnSystemStartup)
			.DoAsync(async _ => await StartupHelper.ModifyStartupSettingAsync(RunOnSystemStartup))
			.Subscribe();

		// Apply DoUpdateOnClose
		this.WhenAnyValue(x => x.DoUpdateOnClose)
			.Do(x => Services.UpdateManager.DoUpdateOnClose = x)
			.Subscribe();

		// Save browser settings
		this.WhenAnyValue(
			x => x.SelectedBrowser,
			x => x.BrowserPath)
			.Skip(1)
			.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
			.Do(_ =>
			{
				switch (SelectedBrowser)
				{
					case BrowserTypeDropdownListEnum.SystemDefault:
						_uiConfig.SelectedBrowser = "";
						BrowserPath = "";
						break;

					case BrowserTypeDropdownListEnum.Custom:
						_uiConfig.SelectedBrowser = BrowserPath;
						break;

					default:
						{
							BrowserPath = "";
							if (Enum.TryParse<BrowserType>(SelectedBrowser.ToString(), out var result))
							{
								_uiConfig.SelectedBrowser = result.ToString();
							}
							else
							{
								// Something went wrong, use default.
								_uiConfig.SelectedBrowser = "";
							}
						}
						break;
				}
			})
			.Subscribe();
	}

	private BrowserTypeDropdownListEnum GetSelectedBrowser()
	{
		if (string.IsNullOrWhiteSpace(_uiConfig.SelectedBrowser))
		{
			return BrowserTypeDropdownListEnum.SystemDefault;
		}
		else if (Enum.TryParse(_uiConfig.SelectedBrowser, out BrowserTypeDropdownListEnum browserType))
		{
			return browserType switch
			{
				BrowserTypeDropdownListEnum.SystemDefault or BrowserTypeDropdownListEnum.Custom => BrowserTypeDropdownListEnum.SystemDefault,
				_ => browserType,
			};
		}

		return BrowserTypeDropdownListEnum.Custom;
	}

	public bool IsOverridden => _config.IsOverridden;

	public IObservable<bool> IsRestartNeeded => _isRestartNeeded;

	public bool CheckIfRestartIsNeeded(PersistentConfig config)
	{
		return !_startupConfig.DeepEquals(config) || _twoFactorAuthentication.StartupValue != _twoFactorAuthentication.TwoFactorEnabled;
	}

	public TorMode GetTorStartupMode()
	{
		return Config.ObjectToTorMode(_startupConfig.UseTor);
	}

	private void Save()
	{
		RxApp.MainThreadScheduler.Schedule(
			() =>
			{
				try
				{
					PersistentConfig currentConfig = ConfigManagerNg.LoadFile<PersistentConfig>(_persistentConfigFilePath);
					PersistentConfig newConfig = ApplyChanges(currentConfig);
					ConfigManagerNg.ToFile(_persistentConfigFilePath, newConfig);

					_isRestartNeeded.OnNext(CheckIfRestartIsNeeded(newConfig));
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
			});
	}

	private PersistentConfig ApplyChanges(PersistentConfig config)
	{
		PersistentConfig result = config;

		// Advanced
		result = result with { EnableGpu = EnableGpu };

		// Bitcoin
		if (Network == config.Network)
		{
			if (EndPointParser.TryParse(BitcoinP2PEndPoint, Network.DefaultPort, out EndPoint? endPoint))
			{
				if (Network == Network.Main)
				{
					result = result with { MainNetBitcoinP2pEndPoint = endPoint };
				}
				else if (Network == Network.TestNet)
				{
					result = result with { TestNetBitcoinP2pEndPoint = endPoint };
				}
				else if (Network == Network.RegTest)
				{
					result = result with { RegTestBitcoinP2pEndPoint = endPoint };
				}
				else
				{
					throw new NotSupportedNetworkException(Network);
				}
			}

			if (Network == Network.Main)
			{
				result = result with { MainNetCoordinatorUri = CoordinatorUri };
			}
			else if (Network == Network.TestNet)
			{
				result = result with { TestNetCoordinatorUri = CoordinatorUri };
			}
			else if (Network == Network.RegTest)
			{
				result = result with { RegTestCoordinatorUri = CoordinatorUri };
			}
			else
			{
				throw new NotSupportedNetworkException(Network);
			}

			result = result with
			{
				StartLocalBitcoinCoreOnStartup = StartLocalBitcoinCoreOnStartup,
				StopLocalBitcoinCoreOnShutdown = StopLocalBitcoinCoreOnShutdown,
				LocalBitcoinCoreDataDir = Guard.Correct(LocalBitcoinCoreDataDir),
				DustThreshold = decimal.TryParse(DustThreshold, out var threshold)
					? Money.Coins(threshold)
					: PersistentConfig.DefaultDustThreshold,
			};
		}
		else
		{
			result = result with
			{
				Network = Network
			};

			BitcoinP2PEndPoint = result.GetBitcoinP2pEndPoint().ToString(defaultPort: -1);
			CoordinatorUri = result.GetCoordinatorUri();
		}

		// General
		result = result with
		{
			UseTor = UseTor.ToString(),
			TerminateTorOnExit = TerminateTorOnExit,
			DownloadNewVersion = DownloadNewVersion,
			DisplayLanguage = (int)SelectedDisplayLanguage,
		};

		return result;
	}

	private void ApplyUiConfigChanges()
	{
		_uiConfig.DarkModeEnabled = DarkModeEnabled;
		_uiConfig.Autocopy = AutoCopy;
		_uiConfig.AutoPaste = AutoPaste;
		_uiConfig.IsCustomChangeAddress = CustomChangeAddress;
		_uiConfig.FeeDisplayUnit = (int)SelectedFeeDisplayUnit;
		_uiConfig.RunOnSystemStartup = RunOnSystemStartup;
		_uiConfig.HideOnClose = HideOnClose;
		_uiConfig.Oobe = Oobe;
		_uiConfig.WindowState = WindowState.ToString();
		_uiConfig.BuySellConfiguration = BuySellConfiguration;
	}

	public void SetBuyCountry(CountrySelection country)
	{
		var current = BuySellConfiguration;

		current = current with { BuyCountry = country };

		if (current.SellCountry is null)
		{
			current = current with { SellCountry = country };
		}

		BuySellConfiguration = current;
	}

	public void SetSellCountry(CountrySelection country)
	{
		var current = BuySellConfiguration;

		current = current with { SellCountry = country };

		if (current.BuyCountry is null)
		{
			current = current with { BuyCountry = country };
		}

		BuySellConfiguration = current;
	}

	public CountrySelection? GetCurrentBuyCountry()
	{
		return BuySellConfiguration.BuyCountry;
	}

	public CountrySelection? GetCurrentSellCountry()
	{
		return BuySellConfiguration.SellCountry;
	}

	public CurrencyModel? GetCurrentBuyCurrency()
	{
		return BuySellConfiguration.BuyCurrency;
	}

	public CurrencyModel? GetCurrentSellCurrency()
	{
		return BuySellConfiguration.SellCurrency;
	}

	public void SetBuyCurrency(CurrencyModel currency)
	{
		var current = BuySellConfiguration;
		current = current with { BuyCurrency = currency };

		BuySellConfiguration = current;
	}

	public void SetSellCurrency(CurrencyModel currency)
	{
		var current = BuySellConfiguration;
		current = current with { SellCurrency = currency };

		BuySellConfiguration = current;
	}

	private void ApplyUiConfigPrivacyModeChange()
	{
		_uiConfig.PrivacyMode = PrivacyMode;
	}
}
