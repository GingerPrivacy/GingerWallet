using System.Threading.Tasks;
using System.Threading;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation;

public interface IWalletFeeRateProvider
{
	public AllFeeEstimate GetAllFeeEstimate();

	public bool IsFastRefresh { get; set; }
}
