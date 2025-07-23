using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using WalletWasabi.Bases;
using WalletWasabi.Daemon.BuySell;

namespace WalletWasabi.Daemon;

[JsonObject(MemberSerialization.OptIn)]
public class UiConfig : ConfigBase
{
	private bool _privacyMode;
	private bool _isCustomChangeAddress;
	private bool _autocopy;
	private int _feeDisplayUnit;
	private bool _darkModeEnabled;
	private string? _lastSelectedWallet;
	private string _windowState = "Normal";
	private bool _runOnSystemStartup;
	private bool _oobe;
	private bool _hideOnClose;
	private bool _autoPaste;
	private int _feeTarget;
	private bool _sendAmountConversionReversed;
	private double? _windowWidth;
	private double? _windowHeight;
	private string _selectedBrowser = "";
	private BuySellConfiguration _buySellConfiguration = new ();
	private DefaultCommands _defaultCommands = new();

	public UiConfig() : base()
	{
	}

	public UiConfig(string filePath)
	{
		SetFilePath(filePath);
	}

	[JsonProperty(PropertyName = "Oobe", DefaultValueHandling = DefaultValueHandling.Populate)]
	[DefaultValue(true)]
	public bool Oobe
	{
		get => _oobe;
		set => RaiseAndSetIfChanged(ref _oobe, value);
	}

	[JsonProperty(PropertyName = "WindowState")]
	public string WindowState
	{
		get => _windowState;
		set => RaiseAndSetIfChanged(ref _windowState, value);
	}

	[DefaultValue(2)]
	[JsonProperty(PropertyName = "FeeTarget", DefaultValueHandling = DefaultValueHandling.Populate)]
	public int FeeTarget
	{
		get => _feeTarget;
		set => RaiseAndSetIfChanged(ref _feeTarget, value);
	}

	[DefaultValue(0)]
	[JsonProperty(PropertyName = "FeeDisplayUnit", DefaultValueHandling = DefaultValueHandling.Populate)]
	public int FeeDisplayUnit
	{
		get => _feeDisplayUnit;
		set => RaiseAndSetIfChanged(ref _feeDisplayUnit, value);
	}

	[DefaultValue("")]
	[JsonProperty(PropertyName = "SelectedBrowser", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string SelectedBrowser
	{
		get => _selectedBrowser;
		set => RaiseAndSetIfChanged(ref _selectedBrowser, value);
	}

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "Autocopy", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool Autocopy
	{
		get => _autocopy;
		set => RaiseAndSetIfChanged(ref _autocopy, value);
	}

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "AutoPaste", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool AutoPaste
	{
		get => _autoPaste;
		set => RaiseAndSetIfChanged(ref _autoPaste, value);
	}

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "IsCustomChangeAddress", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool IsCustomChangeAddress
	{
		get => _isCustomChangeAddress;
		set => RaiseAndSetIfChanged(ref _isCustomChangeAddress, value);
	}

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "PrivacyMode", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool PrivacyMode
	{
		get => _privacyMode;
		set => RaiseAndSetIfChanged(ref _privacyMode, value);
	}

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "DarkModeEnabled", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool DarkModeEnabled
	{
		get => _darkModeEnabled;
		set => RaiseAndSetIfChanged(ref _darkModeEnabled, value);
	}

	[DefaultValue(null)]
	[JsonProperty(PropertyName = "LastSelectedWallet", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string? LastSelectedWallet
	{
		get => _lastSelectedWallet;
		set => RaiseAndSetIfChanged(ref _lastSelectedWallet, value);
	}

	// OnDeserialized changes this default on Linux.
	[DefaultValue(true)]
	[JsonProperty(PropertyName = "RunOnSystemStartup", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool RunOnSystemStartup
	{
		get => _runOnSystemStartup;
		set => RaiseAndSetIfChanged(ref _runOnSystemStartup, value);
	}

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "HideOnClose", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool HideOnClose
	{
		get => _hideOnClose;
		set => RaiseAndSetIfChanged(ref _hideOnClose, value);
	}

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "SendAmountConversionReversed", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool SendAmountConversionReversed
	{
		get => _sendAmountConversionReversed;
		set => RaiseAndSetIfChanged(ref _sendAmountConversionReversed, value);
	}

	[JsonProperty(PropertyName = "WindowWidth")]
	public double? WindowWidth
	{
		get => _windowWidth;
		set => RaiseAndSetIfChanged(ref _windowWidth, value);
	}

	[JsonProperty(PropertyName = "WindowHeight")]
	public double? WindowHeight
	{
		get => _windowHeight;
		set => RaiseAndSetIfChanged(ref _windowHeight, value);
	}

	[JsonProperty(PropertyName = "BuySellConfiguration")]
	public BuySellConfiguration BuySellConfiguration
	{
		get => _buySellConfiguration;
		set => RaiseAndSetIfChanged(ref _buySellConfiguration, value);
	}

	[JsonProperty(PropertyName = "DefaultCommands")]
	public DefaultCommands DefaultCommands
	{
		get => _defaultCommands;
		set => RaiseAndSetIfChanged(ref _defaultCommands, value);
	}

	[OnDeserialized]
	internal void OnDeserialized(StreamingContext context)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) // On win this works perfectly. By default Ginger will run after startup.
		{
			return;
		}

		if (!Oobe) // We do not touch anything if it is not the first run.
		{
			return;
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) // On Linux we do not start Ginger with OS by default - because Linux users knows better.
		{
			RunOnSystemStartup = false;
		}
	}
}
