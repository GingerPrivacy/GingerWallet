using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.Extensions;
using WalletWasabi.Models;
using WalletWasabi.JsonConverters;
using System.Runtime.Serialization;
using System.Linq;

namespace WalletWasabi.Blockchain.Keys;

[JsonObject(MemberSerialization.OptIn)]
public class WalletAttributes
{
	public const bool DefaultAutoCoinjoin = false;
	public const int DefaultAnonScoreTarget = 5;
	public const bool DefaultRedCoinIsolation = false;
	public const int DefaultSafeMiningFeeRate = 10;
	public const int DefaultFeeRateMedianTimeFrameHours = 0;
	public static readonly Money DefaultPlebStopThreshold = Money.Coins(0.01m);

	public WalletAttributes()
	{
	}

	private KeyManager? _keyManager;

	internal KeyManager? KeyManager
	{
		get => _keyManager;
		set
		{
			_keyManager = value;
			if (_keyManager is not null)
			{
				HdPubKeys?.ForEach(_keyManager.AddOrUpdateKey);
			}
		}
	}

	[JsonProperty(PropertyName = "AutoCoinJoin")]
	public bool AutoCoinJoin { get; set; } = DefaultAutoCoinjoin;

	/// <summary>
	/// Won't coinjoin automatically if the wallet balance is less than this.
	/// </summary>
	[JsonProperty(PropertyName = "PlebStopThreshold")]
	[JsonConverter(typeof(MoneyBtcJsonConverter))]
	public Money PlebStopThreshold { get; set; } = DefaultPlebStopThreshold;

	[JsonProperty(PropertyName = "Icon")]
	public string? Icon { get; internal set; }

	[JsonProperty(PropertyName = "AnonScoreTarget")]
	public int AnonScoreTarget { get; set; } = DefaultAnonScoreTarget;

	[JsonProperty(PropertyName = "SafeMiningFeeRate")]
	public int SafeMiningFeeRate { get; set; } = DefaultSafeMiningFeeRate;

	[JsonProperty(PropertyName = "FeeRateMedianTimeFrameHours")]
	public int FeeRateMedianTimeFrameHours { get; internal set; } = DefaultFeeRateMedianTimeFrameHours;

	[JsonProperty(PropertyName = "IsCoinjoinProfileSelected")]
	public bool IsCoinjoinProfileSelected { get; set; } = false;

	[JsonProperty(PropertyName = "RedCoinIsolation")]
	public bool RedCoinIsolation { get; set; } = DefaultRedCoinIsolation;

	[JsonProperty(PropertyName = "CoinjoinSkipFactors")]
	public CoinjoinSkipFactors CoinjoinSkipFactors { get; set; } = CoinjoinSkipFactors.SpeedMaximizing;

	[JsonProperty(ItemConverterType = typeof(Uint256JsonConverter), PropertyName = "CoinJoinTransactions")]
	public List<uint256> CoinJoinTransactions { get; internal set; } = new();

	[JsonProperty(ItemConverterType = typeof(OutPointJsonConverter), Order = 800, PropertyName = "ExcludedCoinsFromCoinJoin")]
	public List<OutPoint> ExcludedCoinsFromCoinJoin { get; internal set; } = new();

	[JsonProperty(ItemConverterType = typeof(OutPointJsonConverter), Order = 900, PropertyName = "CoinJoinOutputs")]
	public List<OutPoint> CoinJoinOutputs { get; internal set; } = new();

	[JsonProperty(Order = 999, PropertyName = "HdPubKeys")]
	private List<HdPubKey> HdPubKeys { get; } = new();

	[OnSerializing]
	private void OnSerializingMethod(StreamingContext context)
	{
		HdPubKeys.Clear();
		if (KeyManager is not null)
		{
			var list = KeyManager.HdPubKeyCache.HdPubKeys.Where(x => x.Labels.Count > 0 || x.KeyState == KeyState.Locked).ToHashSet().OrderBy(x => x.FullKeyPath, new NBitcoinExtensions.KeyPathComparer());
			HdPubKeys.AddRange(list);
		}
	}
}
