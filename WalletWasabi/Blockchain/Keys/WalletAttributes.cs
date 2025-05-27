using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Extensions;
using WalletWasabi.Models;
using System.Linq;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using System.Text.Json.Serialization;

namespace WalletWasabi.Blockchain.Keys;

public class WalletAttributes : IJsonOnSerializing
{
	public const bool DefaultAutoCoinjoin = false;
	public const int DefaultAnonScoreTarget = 20;
	public const bool DefaultRedCoinIsolation = false;
	public const int DefaultSafeMiningFeeRate = 10;
	public const int DefaultFeeRateMedianTimeFrameHours = 0;
	public static readonly Money DefaultPlebStopThreshold = Money.Coins(0.003m);

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

	public bool AutoCoinJoin { get; set; } = DefaultAutoCoinjoin;

	/// <summary>
	/// Won't coinjoin automatically if the wallet balance is less than this.
	/// </summary>
	public Money PlebStopThreshold { get; set; } = DefaultPlebStopThreshold;

	[JsonInclude]
	public string? Icon { get; internal set; }

	public int AnonScoreTarget { get; set; } = DefaultAnonScoreTarget;

	public int SafeMiningFeeRate { get; set; } = DefaultSafeMiningFeeRate;

	[JsonInclude]
	public int FeeRateMedianTimeFrameHours { get; internal set; } = DefaultFeeRateMedianTimeFrameHours;

	public bool IsCoinjoinProfileSelected { get; set; } = false;

	public bool RedCoinIsolation { get; set; } = DefaultRedCoinIsolation;

	public CoinjoinSkipFactors CoinjoinSkipFactors { get; set; } = CoinjoinSkipFactors.NoSkip;

	public BuySellWalletData BuySellWalletData { get; set; } = new();

	public CoinJoinCoinSelectionSettings CoinJoinCoinSelectionSettings { get; set; } = new();

	[JsonInclude]
	public List<uint256> CoinJoinTransactions { get; internal set; } = new();

	[JsonInclude]
	public List<OutPoint> ExcludedCoinsFromCoinJoin { get; internal set; } = new();

	[JsonInclude]
	public List<OutPoint> CoinJoinOutputs { get; internal set; } = new();

	[JsonInclude]
	private List<HdPubKey> HdPubKeys { get; set; } = new();

	public void OnSerializing()
	{
		HdPubKeys.Clear();
		if (KeyManager is not null)
		{
			var list = KeyManager.HdPubKeyCache.HdPubKeys.Where(x => x.Labels.Count > 0 || x.KeyState == KeyState.Locked).ToHashSet().OrderBy(x => x.FullKeyPath, new NBitcoinExtensions.KeyPathComparer());
			HdPubKeys.AddRange(list);
		}
	}
}
