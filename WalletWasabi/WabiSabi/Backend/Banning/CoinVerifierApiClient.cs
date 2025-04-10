using NBitcoin;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifierApiClient : IAsyncDisposable
{
	public CoinVerifierApiClient(HttpClient httpClient, CoinVerifierConfig config)
	{
		_provider = WabiSabiBackendFactory.Instance.CreateCoinVerifierProvider(httpClient, config);
	}

	// For mock testing
	public CoinVerifierApiClient(HttpClient httpClient, CoinVerifierProvider provider)
	{
		_provider = provider;
	}

	public async Task<ApiResponse> SendRequestAsync(Coin coin, int coinBlockHeight, int currentBlockHeight, CancellationToken cancellationToken)
	{
		return await _provider.SendRequestAsync(coin, coinBlockHeight, currentBlockHeight, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		_provider.Dispose();
		return ValueTask.CompletedTask;
	}

	private CoinVerifierProvider _provider;
}
