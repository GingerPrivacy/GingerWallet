using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Logging;

namespace WalletWasabi.Daemon.FeeRateProviders;

public class FallbackFeeRateProvider(params IFeeRateProvider[] providers) : IFeeRateProvider
{
	public async Task<AllFeeEstimate> GetFeeRatesAsync(CancellationToken cancellationToken)
	{
		Exception? lastError = null;
		foreach (var provider in providers)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				var estimate = await provider.GetFeeRatesAsync(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
				cancellationToken.ThrowIfCancellationRequested();
				if (estimate.Estimations.Count == 0 || estimate.Estimations.Values.Any(x => x.SatoshiPerByte <= 0))
				{
					throw new InvalidOperationException("The fee provider returned no usable estimates.");
				}

				return estimate;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				lastError = ex;
				Logger.LogWarning($"Fee estimates from {provider.GetType().Name} failed: {ex.Message}");
			}
		}

		throw new InvalidOperationException("Fee estimates are unavailable from all configured providers.", lastError);
	}
}
