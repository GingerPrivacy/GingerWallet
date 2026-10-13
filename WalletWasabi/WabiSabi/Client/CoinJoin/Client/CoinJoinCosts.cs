using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using NBitcoin;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client;

public record CoinJoinCosts(Money MiningFee, Money CoordinationFee, Money WastedDust, Money PaymentsTotal)
{
	[JsonIgnore]
	public Money TotalFee => MiningFee + CoordinationFee + WastedDust;

	[JsonIgnore]
	public bool IsValid => MiningFee is { } && CoordinationFee is { } && WastedDust is { } && PaymentsTotal is { }
		&& MiningFee >= Money.Zero && CoordinationFee >= Money.Zero && WastedDust >= Money.Zero && PaymentsTotal >= Money.Zero;

	public static CoinJoinCosts? Calculate(FeeRate miningFeeRate, CoordinationFeeRate coordinationFeeRate,
		IEnumerable<(Coin Coin, bool FeeExempt)> inputs, IEnumerable<TxOut> outputs, IEnumerable<TxOut>? transactionOutputs = null)
	{
		var myInputs = inputs.ToArray();
		var expectedOutputs = outputs.ToArray();
		var myScripts = expectedOutputs.Select(x => x.ScriptPubKey).ToHashSet();
		var myOutputs = (transactionOutputs ?? expectedOutputs).Where(x => myScripts.Contains(x.ScriptPubKey)).ToArray();
		if (myInputs.Length == 0 || myOutputs.Length == 0)
		{
			return null;
		}
		if (!myScripts.IsSubsetOf(myOutputs.Select(x => x.ScriptPubKey).ToHashSet()))
		{
			return null;
		}
		var miningFee = myInputs.Sum(x => miningFeeRate.GetFee(x.Coin.ScriptPubKey.EstimateInputVsize()))
			+ myOutputs.Sum(x => miningFeeRate.GetFee(x.ScriptPubKey.EstimateOutputVsize()));
		var coordinationFee = myInputs.Where(x => !x.FeeExempt).Sum(x => coordinationFeeRate.GetFee(x.Coin.Amount));
		var dust = myInputs.Sum(x => x.Coin.Amount) - myOutputs.Sum(x => x.Value) - miningFee - coordinationFee;
		var costs = new CoinJoinCosts(miningFee, coordinationFee, dust, Money.Zero);
		return costs.IsValid ? costs : null;
	}

	public static CoinJoinCosts? Aggregate(IEnumerable<CoinJoinCosts?> costs)
	{
		var entries = costs.ToArray();
		if (entries.Length == 0 || entries.Any(x => x is null || !x.IsValid))
		{
			return null;
		}
		return new CoinJoinCosts(entries.Sum(x => x!.MiningFee), entries.Sum(x => x!.CoordinationFee),
			entries.Sum(x => x!.WastedDust), entries.Sum(x => x!.PaymentsTotal));
	}
}
