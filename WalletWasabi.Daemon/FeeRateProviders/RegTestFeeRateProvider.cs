using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;

namespace WalletWasabi.Daemon.FeeRateProviders;

public class RegTestFeeRateProvider : IFeeRateProvider
{
	private AllFeeEstimate _feeEstimate;

	public RegTestFeeRateProvider()
	{
		_feeEstimate = GetFakeRegTestFeeRatesAsync();
	}

	private static AllFeeEstimate GetFakeRegTestFeeRatesAsync()
	{
		var feeEstimations = new Dictionary<int, int>
		{
			{ 2, 100 },   // For confirmation target 1, fee rate is 100 sats/vByte
			{ 3, 70 },
			{ 6, 40 },
			{ 72, 10 }
		};

		// Initialize the AllFeeEstimate instance with the dictionary.
		var allFeeEstimate = new AllFeeEstimate(feeEstimations);

		// Gets the fee estimations: int: fee target, int: satoshi/vByte
		return allFeeEstimate;
	}

	public Task<AllFeeEstimate> GetFeeRatesAsync(CancellationToken cancellationToken) => Task.FromResult(_feeEstimate);
}
