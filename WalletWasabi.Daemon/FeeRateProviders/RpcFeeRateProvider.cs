using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Extensions;

namespace WalletWasabi.Daemon.FeeRateProviders;

public class RpcFeeRateProvider : IFeeRateProvider
{
	private readonly IRPCClient _rpcClient;

	public RpcFeeRateProvider(IRPCClient rpcClient)
	{
		_rpcClient = rpcClient;
	}

	public async Task<AllFeeEstimate> GetFeeRatesAsync(CancellationToken cancellationToken)
	{
		return await _rpcClient.EstimateAllFeeAsync(cancellationToken).ConfigureAwait(false);
	}
}
