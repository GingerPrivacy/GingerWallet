using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifierApiClient : IAsyncDisposable
{
	public CoinVerifierApiClient(HttpClient httpClient, IRPCClient rpcClient, IEnumerable<CoinVerifierConfig> configs, string responsePath)
	{
		_providers = configs.Select(config => WabiSabiBackendFactory.Instance.CreateCoinVerifierProvider(httpClient, rpcClient, config, responsePath)).ToArray();
	}

	// For mock testing
	public CoinVerifierApiClient(HttpClient httpClient, CoinVerifierProvider provider)
	{
		_providers = [provider];
	}

	public async Task<ApiResponse> SendRequestAsync(Coin coin, int coinBlockHeight, int currentBlockHeight, CancellationToken cancellationToken)
	{
		ApiResponse? response = null;
		for (int idx = 0; idx < _providers.Length; idx++)
		{
			response = await _providers[idx].SendRequestAsync(coin, coinBlockHeight, currentBlockHeight, cancellationToken).ConfigureAwait(false);
			if (response.ShouldBan || response.ShouldRemove)
			{
				return response;
			}
		}
		response ??= new(ApiResponseInfo.OK, "none", true, true, "No providers", TimeSpan.FromHours(1));
		return response;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		foreach (var provider in _providers)
		{
			provider.Dispose();
		}
		return ValueTask.CompletedTask;
	}

	private CoinVerifierProvider[] _providers;
}
