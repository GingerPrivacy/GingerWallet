using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionSummary
{
	public TransactionSummary(SmartTransaction tx, FeeRate? effectiveFeeRate)
	{
		Transaction = tx;
		EffectiveFeeRate = effectiveFeeRate;
	}

	public SmartTransaction Transaction { get; }

	public int OutputCoinCount { get; private set; } = 0;
	public int InputCoinCount { get; private set; } = 0;

	public Money Amount => OutputAmount - InputAmount;
	public Money OutputAmount { get; private set; } = Money.Zero;
	public Money InputAmount { get; private set; } = Money.Zero;

	public FeeRate? EffectiveFeeRate { get; }

	public DateTimeOffset FirstSeen => Transaction.FirstSeen;
	public LabelsArray Labels => Transaction.Labels;
	public Height Height => Transaction.Height;
	public uint256? BlockHash => Transaction.BlockHash;
	public int BlockIndex => Transaction.BlockIndex;
	public bool IsCancellation => Transaction.IsCancellation;
	public bool IsSpeedup => Transaction.IsSpeedup;
	public bool IsCPFP => Transaction.IsCPFP;
	public bool IsCPFPd => Transaction.IsCPFPd;

	public Money? GetFee() => Transaction.GetFee();

	public FeeRate? FeeRate() => Transaction.TryGetFeeRate(out var feeRate) ? feeRate : EffectiveFeeRate;

	public uint256 GetHash() => Transaction.GetHash();

	public bool IsOwnCoinjoin() => Transaction.IsOwnCoinjoin();

	public bool IsCoinjoin()
	{
		return InputCoinCount > 0 && InputCoinCount < Transaction.Transaction.Inputs.Count;
	}

	public double ClientInputRatio => Transaction.WalletInputs.Count / (double)Transaction.Transaction.Inputs.Count;

	public void AddOutputCoin(Money amount)
	{
		OutputCoinCount++;
		OutputAmount += amount;
	}

	public void AddInputCoin(Money amount)
	{
		InputCoinCount++;
		InputAmount += amount;
	}
}
