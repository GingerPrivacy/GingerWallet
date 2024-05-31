using NBitcoin;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Banning.CVP1;
using WalletWasabi.WabiSabi.Backend.Banning.CVP2;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifierApiClient : IAsyncDisposable
{
	/// <summary>Maximum number of actual HTTP requests that might be served concurrently by the CoinVerifier webserver.</summary>
	public int MaxParallelRequestCount { get; } = 30;

	/// <summary>Maximum re-tries for a single API request.</summary>
	private const int MaxRetries = 3;

	public CoinVerifierApiClient(CoinVerifierProvider provider, string apiToken, string apiSecret, HttpClient httpClient)
	{
		Provider = provider;
		ApiToken = apiToken;
		ApiSecret = apiSecret;
		HttpClient = httpClient;

		_validStatus = new() { HttpStatusCode.OK };

		switch (Provider)
		{
			case CoinVerifierProvider.CVP1:
				{
					CreateRequest = CreateRequestCVP1;
					ParseResponse = CVP1ApiResponse.ParseResponseAsync;
					break;
				}
			case CoinVerifierProvider.CVP2:
				{
					CreateRequest = CreateRequestCVP2;
					ParseResponse = CVP2ApiResponse.ParseResponseAsync;

					MaxParallelRequestCount = 4;
					_messageSigner = new HMACSHA256(Convert.FromBase64String(ApiSecret));
					_validStatus.Add(HttpStatusCode.NotFound);
					break;
				}
			default:
				throw new InvalidOperationException("Unknown CoinVerifierProvider type.");
		}

		ThrottlingSemaphore = new(initialCount: MaxParallelRequestCount);

		if (HttpClient.BaseAddress is null)
		{
			throw new HttpRequestException($"{nameof(HttpClient.BaseAddress)} was null.");
		}

		if (HttpClient.BaseAddress.Scheme != "https")
		{
			throw new HttpRequestException($"The connection to the API is not safe. Expected https but was {HttpClient.BaseAddress.Scheme}.");
		}

		_apiHttpPath = HttpClient.BaseAddress?.LocalPath ?? "";
	}

	/// <summary>Long timeout for a single API request. No retry after that. </summary>
	public static TimeSpan ApiRequestTimeout { get; } = TimeSpan.FromMinutes(5);

	public Func<Coin, HttpRequestMessage> CreateRequest { get; }
	public Func<HttpResponseMessage, Task<ApiResponse>> ParseResponse { get; }

	private CoinVerifierProvider Provider { get; }

	private string ApiToken { get; }
	private string ApiSecret { get; }
	private List<HttpStatusCode> _validStatus;

	private HttpClient HttpClient { get; }
	private SemaphoreSlim ThrottlingSemaphore { get; }

	public virtual async Task<ApiResponse> SendRequestAsync(Coin coin, CancellationToken cancellationToken)
	{
		HttpResponseMessage? response = null;

		for (int i = 0; i < MaxRetries; i++)
		{
			try
			{
				// Makes sure that there are no more than MaxParallelRequestCount requests in-flight at a time.
				// Re-tries are not an exception to the max throttling limit.
				await ThrottlingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

				using var content = CreateRequest.Invoke(coin);
				try
				{
					using CancellationTokenSource apiTimeoutCts = new(ApiRequestTimeout);
					using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(apiTimeoutCts.Token, cancellationToken);
					response = await HttpClient.SendAsync(content, linkedCts.Token).ConfigureAwait(false);
				}
				finally
				{
					ThrottlingSemaphore.Release();
				}

				if (_validStatus.Contains(response.StatusCode))
				{
					// Successful request, break the iteration.
					break;
				}
				var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				throw new InvalidOperationException($"HTTP status code was {response.StatusCode}: {responseString}.");
			}
			catch (OperationCanceledException)
			{
				Logger.LogWarning($"API request timed out for script: {coin.ScriptPubKey}.");
				throw;
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"API request failed for script ({response?.StatusCode}): {coin.ScriptPubKey}. Remaining tries: {i}. Exception: {ex}.");
				// CVP2 has a relatively low rate limit of calls/sec, this code handles that as well
				await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
			}
		}

		// Handle the HTTP response, if there is any.
		if (response?.StatusCode == HttpStatusCode.Forbidden)
		{
			throw new UnauthorizedAccessException("User roles access forbidden.");
		}
		else if (response == null || !_validStatus.Contains(response.StatusCode))
		{
			throw new InvalidOperationException($"API request failed. {nameof(HttpStatusCode)} was {response?.StatusCode}.");
		}

		var result = await ParseResponse(response).ConfigureAwait(false);

		return result;
	}

	private HttpRequestMessage CreateRequestCVP1(Coin coin)
	{
		var address = coin.ScriptPubKey.GetDestinationAddress(Network.Main); // API provider doesn't accept testnet/regtest addresses.
		HttpRequestMessage request = new(HttpMethod.Get, $"{HttpClient.BaseAddress}{address}");
		request.Headers.Authorization = new("Bearer", ApiToken);
		return request;
	}

	private HMACSHA256? _messageSigner = null;
	private ASCIIEncoding _asciiEncoder = new();
	private string _apiHttpPath;

	private HttpRequestMessage CreateRequestCVP2(Coin coin)
	{
		var address = coin.ScriptPubKey.GetDestinationAddress(Network.Main);
		string body = $$"""{"subject":{"asset":"holistic","blockchain":"holistic","type":"address","hash":"{{address}}"},"type":"wallet_exposure"}""";
		string time = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();

		// Generate the sign
		byte[] msgBytes = _asciiEncoder.GetBytes($"{time}POST{_apiHttpPath}{body}");
		string sign = _messageSigner != null ? Convert.ToBase64String(_messageSigner.ComputeHash(msgBytes)) : "";

		var request = new HttpRequestMessage
		{
			Method = HttpMethod.Post,
			RequestUri = HttpClient.BaseAddress,
			Headers =
			{
				{ "accept", "application/json" },
				{ "x-access-key", ApiToken },
				{ "x-access-sign", sign },
				{ "x-access-timestamp", time },
			},
			Content = new StringContent(body)
			{
				Headers =
				{
					ContentType = new MediaTypeHeaderValue("application/json")
				}
			}
		};
		return request;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		ThrottlingSemaphore.Dispose();
		_messageSigner?.Dispose();

		return ValueTask.CompletedTask;
	}
}
