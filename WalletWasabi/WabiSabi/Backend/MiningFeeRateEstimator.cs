using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Backend;

// Class to handle the mining fee rate related tasks
public class MiningFeeRateEstimator
{
	public MiningFeeRateEstimator(WabiSabiConfig config, IRPCClient rpc)
	{
		Config = config;
		Rpc = rpc;
	}

	protected WabiSabiConfig Config { get; }
	protected IRPCClient Rpc { get; }

	public virtual async Task<FeeRate> GetRoundFeeRateAsync(CancellationToken cancellationToken)
	{
		var feeRate = (await Rpc.EstimateConservativeSmartFeeAsync((int)Config.ConfirmationTarget, cancellationToken).ConfigureAwait(false)).FeeRate;
		return feeRate;
	}

	public virtual Task LogMiningFeeRates(bool force, CancellationToken cancel)
	{
		return Task.CompletedTask;
	}
}
