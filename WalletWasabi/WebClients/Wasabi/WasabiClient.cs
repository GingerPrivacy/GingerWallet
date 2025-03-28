using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Models;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WebClients.Wasabi;

public class WasabiClient
{
	public WasabiClient(IHttpClient httpClient)
	{
		HttpClient = httpClient;
	}

	private IHttpClient HttpClient { get; }

	public static Dictionary<uint256, Transaction> TransactionCache { get; } = new();
	private static Queue<uint256> TransactionIdQueue { get; } = new();
	public static object TransactionCacheLock { get; } = new();
	public static ushort ApiVersion { get; private set; } = ushort.Parse(Helpers.Constants.BackendMajorVersion);

	#region batch

	/// <remarks>
	/// Throws OperationCancelledException if <paramref name="cancel"/> is set.
	/// </remarks>
	public async Task<SynchronizeResponse> GetSynchronizeAsync(uint256 bestKnownBlockHash, int count, EstimateSmartFeeMode? estimateMode = null, CancellationToken cancel = default)
	{
		string relativeUri = $"api/v{ApiVersion}/btc/batch/synchronize?bestKnownBlockHash={bestKnownBlockHash}&maxNumberOfFilters={count}&gingerSignature=Ginger";
		if (estimateMode is { })
		{
			relativeUri = $"{relativeUri}&estimateSmartFeeMode={estimateMode}";
		}

		using HttpResponseMessage response = await HttpClient.SendAsync(HttpMethod.Get, relativeUri, cancellationToken: cancel).ConfigureAwait(false);

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		using HttpContent content = response.Content;
		var ret = await content.ReadAsJsonAsync<SynchronizeResponse>().ConfigureAwait(false);

		return ret;
	}

	#endregion batch

	#region blockchain

	/// <remarks>
	/// Throws OperationCancelledException if <paramref name="cancel"/> is set.
	/// </remarks>
	public async Task<FiltersResponse?> GetFiltersAsync(uint256 bestKnownBlockHash, int count, CancellationToken cancel = default)
	{
		using HttpResponseMessage response = await HttpClient.SendAsync(
			HttpMethod.Get,
			$"api/v{ApiVersion}/btc/blockchain/filters?bestKnownBlockHash={bestKnownBlockHash}&count={count}&gingerSignature=Ginger",
	cancellationToken: cancel).ConfigureAwait(false);

		if (response.StatusCode == HttpStatusCode.NoContent)
		{
			return null;
		}

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		using HttpContent content = response.Content;
		var ret = await content.ReadAsJsonAsync<FiltersResponse>().ConfigureAwait(false);

		return ret;
	}

	public async Task<IEnumerable<Transaction>> GetTransactionsAsync(Network network, IEnumerable<uint256> txHashes, CancellationToken cancel)
	{
		var allTxs = new List<Transaction>();
		var txHashesToQuery = new List<uint256>();
		lock (TransactionCacheLock)
		{
			var cachedTxs = TransactionCache.Where(x => txHashes.Contains(x.Key));
			allTxs.AddRange(cachedTxs.Select(x => x.Value));
			txHashesToQuery.AddRange(txHashes.Except(cachedTxs.Select(x => x.Key)));
		}

		foreach (IEnumerable<uint256> chunk in txHashesToQuery.ChunkBy(10))
		{
			cancel.ThrowIfCancellationRequested();

			using HttpResponseMessage response = await HttpClient.SendAsync(
				HttpMethod.Get,
				$"api/v{ApiVersion}/btc/blockchain/transaction-hexes?&transactionIds={string.Join("&transactionIds=", chunk.Select(x => x.ToString()))}",
				cancellationToken: cancel).ConfigureAwait(false);

			if (response.StatusCode != HttpStatusCode.OK)
			{
				await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
			}

			using HttpContent content = response.Content;
			var retString = await content.ReadAsJsonAsync<IEnumerable<string>>().ConfigureAwait(false);
			var ret = retString.Select(x => Transaction.Parse(x, network)).ToList();

			lock (TransactionCacheLock)
			{
				foreach (var tx in ret)
				{
					tx.PrecomputeHash(false, true);
					if (TransactionCache.TryAdd(tx.GetHash(), tx))
					{
						TransactionIdQueue.Enqueue(tx.GetHash());
						if (TransactionCache.Count > 1000) // No more than 1000 txs in cache
						{
							var toRemove = TransactionIdQueue.Dequeue();
							TransactionCache.Remove(toRemove);
						}
					}
				}
			}
			allTxs.AddRange(ret);
		}

		return allTxs.ToDependencyGraph().OrderByDependency();
	}

	public async Task BroadcastAsync(string hex)
	{
		using var content = new StringContent($"'{hex}'", Encoding.UTF8, "application/json");
		using HttpResponseMessage response = await HttpClient.SendAsync(HttpMethod.Post, $"api/v{ApiVersion}/btc/blockchain/broadcast", content).ConfigureAwait(false);

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(CancellationToken.None).ConfigureAwait(false);
		}
	}

	public async Task BroadcastAsync(Transaction transaction)
	{
		await BroadcastAsync(transaction.ToHex()).ConfigureAwait(false);
	}

	public async Task BroadcastAsync(SmartTransaction transaction)
	{
		await BroadcastAsync(transaction.Transaction).ConfigureAwait(false);
	}

	/// <summary>
	/// Gets mempool hashes, but strips the last x characters of each hash.
	/// </summary>
	/// <param name="compactness">1 to 64</param>
	public async Task<ISet<string>> GetMempoolHashesAsync(int compactness, CancellationToken cancel = default)
	{
		using HttpResponseMessage response = await HttpClient.SendAsync(
			HttpMethod.Get,
			$"api/v{ApiVersion}/btc/blockchain/mempool-hashes?compactness={compactness}",
			cancellationToken: cancel).ConfigureAwait(false);

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		using HttpContent content = response.Content;
		var strings = await content.ReadAsJsonAsync<ISet<string>>().ConfigureAwait(false);

		return strings;
	}

	#endregion blockchain

	#region software

	public async Task<(Version ClientVersion, ushort BackendMajorVersion, Version LegalDocumentsVersion)> GetVersionsAsync(CancellationToken cancel)
	{
		using HttpResponseMessage response = await HttpClient.SendAsync(HttpMethod.Get, "api/software/versions", cancellationToken: cancel).ConfigureAwait(false);

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		using HttpContent content = response.Content;
		var resp = await content.ReadAsJsonAsync<VersionsResponse>().ConfigureAwait(false);

		return (Version.Parse(resp.ClientVersion), ushort.Parse(resp.BackendMajorVersion), Version.Parse(resp.GingerLegalDocumentsVersion));
	}

	public async Task<UpdateStatus> CheckUpdatesAsync(CancellationToken cancel)
	{
		var (clientVersion, backendMajorVersion, legalDocumentsVersion) = await GetVersionsAsync(cancel).ConfigureAwait(false);
		var clientUpToDate = Helpers.Constants.ClientVersion >= clientVersion; // If the client version locally is greater than or equal to the backend's reported client version, then good.
		var backendCompatible = int.Parse(Helpers.Constants.ClientSupportBackendVersionMax) >= backendMajorVersion && backendMajorVersion >= int.Parse(Helpers.Constants.ClientSupportBackendVersionMin); // If ClientSupportBackendVersionMin <= backend major <= ClientSupportBackendVersionMax, then our software is compatible.
		var currentBackendMajorVersion = backendMajorVersion;

		if (backendCompatible)
		{
			// Only refresh if compatible.
			ApiVersion = currentBackendMajorVersion;
		}

		return new UpdateStatus(backendCompatible, clientUpToDate, legalDocumentsVersion, currentBackendMajorVersion, clientVersion);
	}

	#endregion software

	#region wasabi

	public async Task<string> GetLegalDocumentsAsync(CancellationToken cancel)
	{
		using HttpResponseMessage response = await HttpClient.SendAsync(
			HttpMethod.Get,
			$"api/v{ApiVersion}/wasabi/legaldocuments?id=gingerwallet",
			cancellationToken: cancel).ConfigureAwait(false);

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		using HttpContent content = response.Content;
		string result = await content.ReadAsStringAsync(cancel).ConfigureAwait(false);

		return result;
	}

	#endregion wasabi

	#region two factor authentication

	public async Task<TwoFactorSetupResponse> SetupTwoFactorAuthenticationAsync(CancellationToken cancel = default)
	{
		try
		{
			using HttpResponseMessage response = await HttpClient.SendAsync(
				HttpMethod.Get,
				$"api/v{ApiVersion}/2fa/setup",
				cancellationToken: cancel).ConfigureAwait(false);

			if (response.StatusCode != HttpStatusCode.OK)
			{
				await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
			}

			using HttpContent content = response.Content;
			string responseBody = await content.ReadAsStringAsync(cancel).ConfigureAwait(false);
			var result = JsonConvert.DeserializeObject<TwoFactorSetupResponse>(responseBody);
			return result is null
				? throw new InvalidOperationException($"Cannot deserialize the response: '{responseBody}'.") : result;
		}
		catch (HttpRequestException e)
		{
			throw new InvalidOperationException($"HTTP request error: {e.Message}", e);
		}
		catch (TaskCanceledException)
		{
			throw new OperationCanceledException("The request was cancelled.");
		}
		catch (JsonException e)
		{
			throw new InvalidOperationException($"JSON deserialization error: {e.Message}", e);
		}
	}

	public async Task<TwoFactorVerifyResponse?> VerifyTwoFactorAuthenticationAsync(VerifyTwoFactorModel verifyTwoFactorModel, CancellationToken cancel = default)
	{
		try
		{
			using var requestContent = new StringContent(
				JsonConvert.SerializeObject(verifyTwoFactorModel),
				Encoding.UTF8,
				"application/json");

			using HttpResponseMessage response = await HttpClient.SendAsync(HttpMethod.Post, $"api/v{ApiVersion}/2fa/verify", requestContent, cancel).ConfigureAwait(false);

			if (response.StatusCode != HttpStatusCode.OK)
			{
				await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
			}

			using HttpContent content = response.Content;
			string responseBody = await content.ReadAsStringAsync(cancel).ConfigureAwait(false);
			var result = JsonConvert.DeserializeObject<TwoFactorVerifyResponse>(responseBody);
			return result is null
				? throw new InvalidOperationException($"Cannot deserialize the response: '{responseBody}'.") : result;
		}
		catch (HttpRequestException e)
		{
			throw new InvalidOperationException($"HTTP request error: {e.Message}", e);
		}
		catch (TaskCanceledException)
		{
			throw new OperationCanceledException("The request was cancelled.");
		}
		catch (JsonException e)
		{
			throw new InvalidOperationException($"JSON deserialization error: {e.Message}", e);
		}
		catch (SecurityException)
		{
			throw;
		}
		catch (Exception e)
		{
			throw new InvalidOperationException($"Error during 2FA verification: {e.Message}", e);
		}
	}

	#endregion two factor authentication
}
