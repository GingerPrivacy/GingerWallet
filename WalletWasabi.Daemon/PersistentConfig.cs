using NBitcoin;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;
using WalletWasabi.Daemon.FeeRateProviders;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.Lang;
using WalletWasabi.Lang.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Daemon;

public record PersistentConfig : IConfigNg
{
	public const int DefaultJsonRpcServerPort = 37128;
	public static readonly Money DefaultDustThreshold = Money.Coins(Constants.DefaultDustThreshold);

	[JsonPropertyName("Network")]
	[JsonConverter(typeof(NetworkJsonConverterNg))]
	public Network Network { get; set; } = Network.Main;

	[DefaultValue(Constants.BackendUri)]
	[JsonPropertyName("MainNetBackendUri")]
	[JsonConverter(typeof(MainNetBackendUriJsonConverter))]
	public string MainNetBackendUri { get; init; } = Constants.BackendUri;

	[DefaultValue(Constants.TestnetBackendUri)]
	[JsonPropertyName("TestNetClearnetBackendUri")]
	[JsonConverter(typeof(TestNetBackendUriJsonConverter))]
	public string TestNetBackendUri { get; init; } = Constants.TestnetBackendUri;

	[DefaultValue("http://localhost:37127/")]
	[JsonPropertyName("RegTestBackendUri")]
	public string RegTestBackendUri { get; init; } = "http://localhost:37127/";

	[DefaultValue(Constants.BackendUri)]
	[JsonPropertyName("MainNetCoordinatorUri")]
	public string MainNetCoordinatorUri { get; init; } = Constants.BackendUri;

	[DefaultValue(Constants.TestnetBackendUri)]
	[JsonPropertyName("TestNetCoordinatorUri")]
	public string TestNetCoordinatorUri { get; init; } = Constants.TestnetBackendUri;

	[DefaultValue("http://localhost:37127/")]
	[JsonPropertyName("RegTestCoordinatorUri")]
	public string RegTestCoordinatorUri { get; init; } = "http://localhost:37127/";

	/// <remarks>
	/// For backward compatibility this was changed to an object.
	/// Only strings (new) and booleans (old) are supported.
	/// </remarks>
	[DefaultValue("Enabled")]
	[JsonPropertyName("UseTor")]
	public object UseTor { get; init; } = "Enabled";

	[DefaultValue(false)]
	[JsonPropertyName("TerminateTorOnExit")]
	public bool TerminateTorOnExit { get; init; } = false;

	[DefaultValue(true)]
	[JsonPropertyName("TorBridges")]
	public string[] TorBridges { get; init; } = [];

	[DefaultValue(true)]
	[JsonPropertyName("DownloadNewVersion")]
	public bool DownloadNewVersion { get; init; } = true;

	[DefaultValue(false)]
	[JsonPropertyName("StartLocalBitcoinCoreOnStartup")]
	public bool StartLocalBitcoinCoreOnStartup { get; init; } = false;

	[DefaultValue(true)]
	[JsonPropertyName("StopLocalBitcoinCoreOnShutdown")]
	public bool StopLocalBitcoinCoreOnShutdown { get; init; } = true;

	[JsonPropertyName("LocalBitcoinCoreDataDir")]
	public string LocalBitcoinCoreDataDir { get; init; } = EnvironmentHelpers.GetDefaultBitcoinCoreDataDirOrEmptyString();

	[JsonPropertyName("MainNetBitcoinP2pEndPoint")]
	[JsonConverter(typeof(MainNetBitcoinP2pEndPointConverterNg))]
	public EndPoint MainNetBitcoinP2pEndPoint { get; init; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultMainNetBitcoinP2pPort);

	[JsonPropertyName("TestNetBitcoinP2pEndPoint")]
	[JsonConverter(typeof(TestNetBitcoinP2pEndPointConverterNg))]
	public EndPoint TestNetBitcoinP2pEndPoint { get; init; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultTestNetBitcoinP2pPort);

	[JsonPropertyName("RegTestBitcoinP2pEndPoint")]
	[JsonConverter(typeof(RegTestBitcoinP2pEndPointConverterNg))]
	public EndPoint RegTestBitcoinP2pEndPoint { get; init; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultRegTestBitcoinP2pPort);

	[DefaultValue(false)]
	[JsonPropertyName("JsonRpcServerEnabled")]
	public bool JsonRpcServerEnabled { get; init; }

	[DefaultValue("")]
	[JsonPropertyName("JsonRpcUser")]
	public string JsonRpcUser { get; init; } = "";

	[DefaultValue("")]
	[JsonPropertyName("JsonRpcPassword")]
	public string JsonRpcPassword { get; init; } = "";

	[JsonPropertyName("JsonRpcServerPrefixes")]
	public string[] JsonRpcServerPrefixes { get; init; } = new[]
	{
		"http://127.0.0.1:37128/",
		"http://localhost:37128/"
	};

	[JsonPropertyName("DustThreshold")]
	[JsonConverter(typeof(MoneyBtcJsonConverterNg))]
	public Money DustThreshold { get; init; } = DefaultDustThreshold;

	[JsonPropertyName("EnableGpu")]
	public bool EnableGpu { get; init; } = true;

	[DefaultValue("CoinJoinCoordinatorIdentifier")]
	[JsonPropertyName("CoordinatorIdentifier")]
	public string CoordinatorIdentifier { get; init; } = "CoinJoinCoordinatorIdentifier";

	[JsonPropertyName("MaxCoordinationFeeRate")]
	public decimal MaxCoordinationFeeRate { get; init; } = Constants.DefaultMaxCoordinationFeeRate;

	[JsonPropertyName("MaxCoinJoinMiningFeeRate")]
	public decimal MaxCoinJoinMiningFeeRate { get; init; } = Constants.DefaultMaxCoinJoinMiningFeeRate;

	[JsonPropertyName("AbsoluteMinInputCount")]
	public int AbsoluteMinInputCount { get; init; } = Constants.DefaultAbsoluteMinInputCount;

	[JsonPropertyName("MaxBlockRepositorySize")]
	public int MaxBlockRepositorySize { get; init; } = Constants.DefaultMaxBlockRepositorySize;

	[JsonPropertyName("Language")]
	[JsonConverter(typeof(DisplayLanguageJsonConverter))]
	public int DisplayLanguage { get; init; } = (int)Models.DisplayLanguage.English;

	[JsonPropertyName("ExchangeCurrency")]
	public string ExchangeCurrency { get; init; } = new CultureInfo(GingerCultureInfo.DefaultLanguage).GuessPreferredCurrencyCode(ExchangeRateService.DefaultCurrencies);

	[JsonPropertyName("GroupSeparator")]
	[JsonConverter(typeof(GroupSeparatorJsonConverter))]
	public string GroupSeparator { get; init; } = LocalizationExtension.GuessPreferredGroupSeparator();

	[JsonPropertyName("DecimalSeparator")]
	[JsonConverter(typeof(DecimalSeparatorJsonConverter))]
	public string DecimalSeparator { get; init; } = LocalizationExtension.GuessPreferredDecimalSeparator();

	[JsonPropertyName("ExtraNostrPubKey")]
	public string ExtraNostrPubKey { get; init; } = "";

	[JsonPropertyName("BtcFractionGroup")]
	public int[] BtcFractionGroup { get; init; } = GingerCultureInfo.DefaultBitcoinFractionSizes;

	[JsonPropertyName("FeeRateEstimationProvider")]
	[JsonConverter(typeof(DefaultingEnumConverter<FeeRateProviderSource>))]
	public FeeRateProviderSource FeeRateEstimationProvider { get; init; } = FeeRateProviderSource.MempoolSpace;

	public bool DeepEquals(PersistentConfig other)
	{
		bool useTorIsEqual = Config.ObjectToTorMode(UseTor) == Config.ObjectToTorMode(other.UseTor);

		return
			Network == other.Network &&
			MainNetBackendUri == other.MainNetBackendUri &&
			TestNetBackendUri == other.TestNetBackendUri &&
			RegTestBackendUri == other.RegTestBackendUri &&
			MainNetCoordinatorUri == other.MainNetCoordinatorUri &&
			TestNetCoordinatorUri == other.TestNetCoordinatorUri &&
			RegTestCoordinatorUri == other.RegTestCoordinatorUri &&
			useTorIsEqual &&
			TerminateTorOnExit == other.TerminateTorOnExit &&
			DownloadNewVersion == other.DownloadNewVersion &&
			StartLocalBitcoinCoreOnStartup == other.StartLocalBitcoinCoreOnStartup &&
			StopLocalBitcoinCoreOnShutdown == other.StopLocalBitcoinCoreOnShutdown &&
			LocalBitcoinCoreDataDir == other.LocalBitcoinCoreDataDir &&
			MainNetBitcoinP2pEndPoint.Equals(other.MainNetBitcoinP2pEndPoint) &&
			TestNetBitcoinP2pEndPoint.Equals(other.TestNetBitcoinP2pEndPoint) &&
			RegTestBitcoinP2pEndPoint.Equals(other.RegTestBitcoinP2pEndPoint) &&
			JsonRpcServerEnabled == other.JsonRpcServerEnabled &&
			JsonRpcUser == other.JsonRpcUser &&
			JsonRpcPassword == other.JsonRpcPassword &&
			JsonRpcServerPrefixes.SequenceEqual(other.JsonRpcServerPrefixes) &&
			DustThreshold == other.DustThreshold &&
			EnableGpu == other.EnableGpu &&
			CoordinatorIdentifier == other.CoordinatorIdentifier &&
			MaxCoordinationFeeRate == other.MaxCoordinationFeeRate &&
			MaxCoinJoinMiningFeeRate == other.MaxCoinJoinMiningFeeRate &&
			AbsoluteMinInputCount == other.AbsoluteMinInputCount &&
			DisplayLanguage == other.DisplayLanguage &&
			GroupSeparator == other.GroupSeparator &&
			DecimalSeparator == other.DecimalSeparator &&
			BtcFractionGroup.SequenceEqual(other.BtcFractionGroup) &&
			ExchangeCurrency == other.ExchangeCurrency &&
			FeeRateEstimationProvider == other.FeeRateEstimationProvider;
	}

	public EndPoint GetBitcoinP2pEndPoint()
	{
		if (Network == Network.Main)
		{
			return MainNetBitcoinP2pEndPoint;
		}
		if (Network == Network.TestNet)
		{
			return TestNetBitcoinP2pEndPoint;
		}
		if (Network == Network.RegTest)
		{
			return RegTestBitcoinP2pEndPoint;
		}
		throw new NotSupportedNetworkException(Network);
	}

	public string GetCoordinatorUri()
	{
		if (Network == Network.Main)
		{
			return MainNetCoordinatorUri;
		}

		if (Network == Network.TestNet)
		{
			return TestNetCoordinatorUri;
		}

		if (Network == Network.RegTest)
		{
			return RegTestCoordinatorUri;
		}

		throw new NotSupportedNetworkException(Network);
	}

	static public bool Migrate(bool readFromWasabi, [NotNullWhen(true)] ref PersistentConfig config)
	{
		bool hasChanged = false;

		if (config.MainNetBackendUri != Constants.BackendUri || config.MainNetCoordinatorUri != Constants.BackendUri)
		{
			hasChanged = true;
			config = config with
			{
				MainNetBackendUri = Constants.BackendUri,
				MainNetCoordinatorUri = Constants.BackendUri
			};
		}

		// Previous imports from Wasabi could increase this to 100. We do want to check and change this, even if we are not readFromWasabi.
		if (config.AbsoluteMinInputCount > Constants.DefaultAbsoluteMinInputCount)
		{
			hasChanged = true;
			config = config with
			{
				AbsoluteMinInputCount = Constants.DefaultAbsoluteMinInputCount
			};
		}

		if (readFromWasabi)
		{
			var torMode = Config.ObjectToTorMode(config.UseTor);
			if (torMode == Models.TorMode.Disabled)
			{
				hasChanged = true;
				config = config with
				{
					UseTor = "Enabled",
				};
			}
			if (config.MaxCoordinationFeeRate < 0.003m)
			{
				hasChanged = true;
				config = config with
				{
					MaxCoordinationFeeRate = 0.003m
				};
			}
		}

		return hasChanged;
	}
}
