using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;

namespace WalletWasabi.Daemon.FeeRateProviders;

public interface IFeeRateProvider
{
	Task<AllFeeEstimate> GetFeeRatesAsync(CancellationToken cancellationToken);
}
