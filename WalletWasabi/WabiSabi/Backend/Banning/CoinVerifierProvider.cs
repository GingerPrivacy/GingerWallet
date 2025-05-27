using NBitcoin;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using GingerCommon.Logging;
using System.IO;
using WalletWasabi.Helpers;
using Microsoft.Extensions.Logging;
using GingerCommon.Crypto.Random;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public abstract class CoinVerifierProvider : IDisposable
{
	public CoinVerifierProvider(HttpClient httpClient, CoinVerifierConfig config, string responseLogPath, int maxParallelRequestCount = 10)
	{
		Config = config;
		ResponseLogPath = responseLogPath;
		MaxParallelRequestCount = maxParallelRequestCount;

		ThrottlingSemaphore = new(initialCount: MaxParallelRequestCount);

		HttpClient = httpClient;
		httpClient.Timeout = ApiRequestTimeout;

		if (!Uri.TryCreate(config.ApiUrl, UriKind.RelativeOrAbsolute, out Uri? url))
		{
			throw new ArgumentException($"API url is invalid in {nameof(WabiSabiConfig)}.");
		}
		if (url.Scheme != "https")
		{
			throw new HttpRequestException($"The connection to the API is not safe. Expected https but was {url.Scheme}.");
		}
		RequestUri = url;

		if (string.IsNullOrEmpty(config.ApiKey))
		{
			throw new ArgumentException($"API key was not provided in {nameof(WabiSabiConfig)}.");
		}
		if (string.IsNullOrEmpty(config.RiskSettings))
		{
			throw new ArgumentException($"Risk settings were not provided in {nameof(WabiSabiConfig)}.");
		}
	}

	/// <summary>Maximum number of actual HTTP requests that might be served concurrently by the CoinVerifier webserver.</summary>
	public int MaxParallelRequestCount { get; }

	/// <summary>Long timeout for a single API request. No retry after that. </summary>
	public TimeSpan ApiRequestTimeout { get; } = TimeSpan.FromMinutes(5);

	/// <summary>Maximum re-tries for a single API request.</summary>
	public int MaxRetries { get; set; } = 3;

	protected HttpClient HttpClient { get; }
	protected CoinVerifierConfig Config { get; }
	protected Uri RequestUri { get; }
	protected string ResponseLogPath { get; }

	private SemaphoreSlim ThrottlingSemaphore { get; }

	public abstract HttpRequestMessage CreateRequest(Coin coin);

	public abstract ApiResponse ParseResponse(HttpStatusCode statusCode, string responseString, Coin coin, int coinBlockHeight, int currentBlockHeight);

	public abstract bool IsValid(HttpResponseMessage response);

	public virtual async Task<HttpResponseMessage> SendRawRequestAsync(Coin coin, int coinBlockHeight, int currentBlockHeight, CancellationToken cancellationToken)
	{
		using var request = CreateRequest(coin);
		request.RequestUri = RequestUri;
		var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
		return response;
	}

	public virtual async Task<ApiResponse> SendRequestAsync(Coin coin, int coinBlockHeight, int currentBlockHeight, CancellationToken cancellationToken)
	{
		HttpResponseMessage? response = null;

		string responseString = "";
		for (int i = 0; i < MaxRetries; i++)
		{
			try
			{
				// Makes sure that there are no more than MaxParallelRequestCount requests in-flight at a time.
				// Re-tries are not an exception to the max throttling limit.
				await ThrottlingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

				try
				{
					using CancellationTokenSource apiTimeoutCts = new(ApiRequestTimeout);
					using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(apiTimeoutCts.Token, cancellationToken);
					response = await SendRawRequestAsync(coin, coinBlockHeight, currentBlockHeight, linkedCts.Token).ConfigureAwait(false);
				}
				finally
				{
					ThrottlingSemaphore.Release();
				}

				responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				if (IsValid(response))
				{
					LogResponse(responseString);
					// Successful request, break the iteration.
					break;
				}
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
				// If the provider has a relatively low rate limit of calls/sec, this code handles that as well
				await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
			}
		}

		// Handle the HTTP response, if there is any.
		if (response?.StatusCode == HttpStatusCode.Forbidden)
		{
			throw new UnauthorizedAccessException("User roles access forbidden.");
		}
		else if (response == null || !IsValid(response))
		{
			throw new InvalidOperationException($"API request failed. {nameof(HttpStatusCode)} was {response?.StatusCode}.");
		}

		var result = ParseResponse(response.StatusCode, responseString, coin, coinBlockHeight, currentBlockHeight);

		return result;
	}

	public void LogResponse(string responseString)
	{
		if (string.IsNullOrWhiteSpace(ResponseLogPath))
		{
			return;
		}

		try
		{
			DateTimeOffset now = DateTimeOffset.UtcNow;
			string name = Config.Name.ToLowerInvariant();
			string path = Path.Combine(ResponseLogPath, $"{now:yyyy_MM}_{name}");
			IoHelpers.EnsureDirectoryExists(path);
			File.WriteAllText(Path.Combine(path, $"{name}_{now:yyyyMMdd_HHmmss_fff}_r{SecureRandom.Instance.GetInt(0, 1000):D3}.txt"), responseString);
		}
		catch (Exception ex)
		{
			Logger.Log("Failed to save the CoinVerifier's response string", ex, LogLevel.Warning);
		}
	}

	public virtual void Dispose()
	{
		ThrottlingSemaphore.Dispose();
	}
}
