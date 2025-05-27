using System.Threading.Tasks;
using System.Threading;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation;

public interface IWalletFeeRateProvider
{
	public AllFeeEstimate GetAllFeeEstimate();

	public Task<AllFeeEstimate> GetAllFeeEstimateAsync(CancellationToken cancellationToken);

	public void TriggerRefresh();
}
