using LinqKit;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Http;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models.Serialization;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.SecretHunt;

public class SecretHuntUpdater : PeriodicRunner
{
	public SecretHuntUpdater(IWalletProvider walletProvider, TimeSpan requestInterval, IWasabiHttpClientFactory httpClientFactory, RoundStateUpdater roundStateUpdater) : base(requestInterval)
	{
		_httpClientFactory = httpClientFactory;
		_roundStateUpdater = roundStateUpdater;

		_httpClient = _httpClientFactory.NewHttpClientWithCircuitPerRequest();
		WalletProvider = walletProvider;
	}

	public SecretHuntEvent[] Events { get; set; } = [];
	public IWalletProvider WalletProvider { get; }

	private RoundStateUpdater _roundStateUpdater;
	private IWasabiHttpClientFactory _httpClientFactory;
	private IHttpClient _httpClient;
	private WasabiRandom _random = SecureRandom.Instance;
	private DateTimeOffset _lastRequestTime;
	private TimeSpan _waitPeriod = TimeSpan.Zero;

	private DateTimeOffset _lastRequestTimeEvents;
	private TimeSpan _waitPeriodEvents = TimeSpan.Zero;

	private SecretHuntEventsRequest _emptyRequest = new();

	private const int MinuteMS = 60000;
	private const int CoinjoinWaitPeriodMin = 5;

	public static readonly JsonSerializerOptions JsonOptions = GetJsonOptions();

	private static JsonSerializerOptions GetJsonOptions()
	{
		JsonSerializerOptions options = new()
		{
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			PropertyNameCaseInsensitive = true
		};
		options.Converters.Add(new GingerCommon.Serialization.JsonConverters.MoneyJsonConverter());
		options.Converters.Add(new GingerCommon.Serialization.JsonConverters.OutPointJsonConverter());
		options.Converters.Add(new GingerCommon.Serialization.JsonConverters.Uint256JsonConverter());
		options.Converters.Add(new OwnershipProofJsonConverterMS());
		// We does not need byte[] converter as the base64 encode is the default behavior

		return options;
	}

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		// Events first
		if (DateTimeOffset.UtcNow - _lastRequestTimeEvents > _waitPeriodEvents)
		{
			try
			{
				var res = await HttpUtils.SendAndReceiveAsync<SecretHuntEventsRequest, SecretHuntEventsResponse>(_httpClient, HttpMethod.Post, "secrethunt/events", _emptyRequest, HttpUtils.RequestBehavior.NonCriticalBehavior, cancel).ConfigureAwait(false);
				Events = res.Events;
			}
			catch
			{
			}
			_lastRequestTimeEvents = DateTimeOffset.UtcNow;
			// We don't need to refresh it frequently
			_waitPeriodEvents = TimeSpan.FromMilliseconds(_random.GetInt(60 * MinuteMS, 120 * MinuteMS));
			return;
		}

		if (DateTimeOffset.UtcNow - _lastRequestTime < _waitPeriod)
		{
			return;
		}

		var roundId = _roundStateUpdater.GetActiveRoundId();
		if (roundId == uint256.Zero)
		{
			// We don't have active round, check back later
			_lastRequestTime = DateTimeOffset.UtcNow;
			_waitPeriod = TimeSpan.FromSeconds(2);
			return;
		}

		List<Wallet> wallets = (await WalletProvider.GetWalletsAsync().ConfigureAwait(false)).OfType<Wallet>()
			.Where(w => w.State == WalletState.Started && !w.KeyManager.IsWatchOnly && w.KeyManager.Attributes.SecretHuntResults.Enabled).ToList();
		if (wallets.Count > 0)
		{
			// Collect all the results we currently have
			Dictionary<string, SecretHuntEventResult> results = CollectResults(wallets);

			var onlyExtraSecretMissing = results.Values.Where(r => r.ExtraSecret is null && r.Event.Weights.Length <= r.Secrets.Count).FirstOrDefault();
			if (onlyExtraSecretMissing is not null)
			{
				uint256 proof = SecretHuntApi.CreateProofOfSecrets(onlyExtraSecretMissing.Event.Id, onlyExtraSecretMissing.Secrets.Keys);
				SecretHuntExtraSecretRequest extraSecretRequest = new(onlyExtraSecretMissing.Event.Id, proof);
				try
				{
					var response = await HttpUtils.SendAndReceiveAsync<SecretHuntExtraSecretRequest, SecretHuntExtraSecretResponse>(_httpClient, HttpMethod.Post, "secrethunt/extrasecret", extraSecretRequest, HttpUtils.RequestBehavior.NonCriticalBehavior, cancel, JsonOptions).ConfigureAwait(false);
					onlyExtraSecretMissing.ExtraSecret = response?.ExtraSecret;
				}
				catch
				{
				}
			}
			else
			{
				await CheckNewTransactionAsync(roundId, wallets, results, cancel).ConfigureAwait(false);
			}

			// Final sort before adding back to the wallets
			results.ForEach(x =>
			{
				x.Value.Secrets.Values.ForEach(x => x.Sort());
				x.Value.CheckedWithNoResults.Sort();
			});
			wallets.ForEach(w => w.KeyManager.UpdateSecretHuntResults(results));
		}

		_lastRequestTime = DateTimeOffset.UtcNow;
		_waitPeriod = TimeSpan.FromMilliseconds(_random.GetInt(CoinjoinWaitPeriodMin * MinuteMS, 2 * CoinjoinWaitPeriodMin * MinuteMS));
	}

	private async Task CheckNewTransactionAsync(uint256 roundId, List<Wallet> wallets, Dictionary<string, SecretHuntEventResult> results, CancellationToken cancel)
	{
		var eventsWithMissingCjs = results.Values.Where(secretHuntEventResult => secretHuntEventResult.ExtraSecret is null);
		if (!eventsWithMissingCjs.Any())
		{
			return;
		}

		// Collect all the coinjoins we can potentially send to the server
		var cjCoins = wallets.SelectMany(x => x.GetAllCoins()).Where(coin => coin.SpenderTransaction is not null && coin.SpenderTransaction.Confirmed && coin.SpenderTransaction.IsWasabi2Cj).
			GroupBy(coin => coin.SpenderTransaction!.GetHash()).Select(g => g.ToList()).ToList();

		DateTimeOffset now = DateTimeOffset.UtcNow;
		// We are interested in not yet resolved events
		Dictionary<SecretHuntEventResult, List<List<SmartCoin>>> candidates = eventsWithMissingCjs
			.Select(secretHuntEventResult => (secretHuntEventResult, cjCoins.Where(coinList => secretHuntEventResult.IsNewCandidate(coinList.First().SpenderTransaction!)).ToList())).ToDictionary();
		// Non-empties
		candidates = candidates.Where(x => x.Value.Count > 0).ToDictionary();
		// Small optimization: for events that are not even started we know that the candidates will be rejected (due to the extra time buffer we added them)
		candidates.Where(x => x.Key.Event.StartDate >= now).ForEach(kvp =>
		{
			kvp.Value.Select(coinList => coinList.First().SpenderTransaction!.GetHash()).Where(txId => !kvp.Key.CheckedWithNoResults.Contains(txId)).ForEach(txId => kvp.Key.CheckedWithNoResults.Add(txId));
		});
		candidates = candidates.Where(kvp => kvp.Key.Event.StartDate < now).ToDictionary();
		if (candidates.Count == 0)
		{
			return;
		}

		List<List<SmartCoin>>? candidate = null;
		if (candidates.Any(x => x.Key.Event.StartDate <= now && x.Key.Event.EndDate >= now))
		{
			// Get an ongoing event
			candidate = candidates.Where(x => x.Key.Event.StartDate <= now && x.Key.Event.EndDate >= now).MinBy(x => x.Key.Event.EndDate).Value;
		}
		else
		{
			// Get the closest event that ended
			candidate = candidates.Where(x => x.Key.Event.StartDate <= now).MaxBy(x => x.Key.Event.EndDate).Value;
		}

		// Get a randon transaction from the candidates
		var txCoins = candidate[_random.GetInt(0, candidates.Count)];
		Money moneyLimit = txCoins.MinBy(coin => coin.Amount)!.Amount * 4;
		txCoins = txCoins.Where(coin => coin.Amount < moneyLimit).ToList();
		var coin = txCoins[_random.GetInt(0, txCoins.Count)];
		SmartTransaction transaction = coin.SpenderTransaction!;
		// TODO: Sending the request
		var wallet = wallets.Find(w => w.GetAllCoins().Contains(coin));
		var ownershipProof = wallet!.KeyChain!.GetOwnershipProof(coin, SecretHuntApi.CreateInputCommitmentData(roundId));
		SecretHuntSecretRequest secretRequest = new(roundId, transaction.GetHash(), coin.Outpoint, ownershipProof);
		try
		{
			var response = await HttpUtils.SendAndReceiveAsync<SecretHuntSecretRequest, SecretHuntSecretResponse>(_httpClient, HttpMethod.Post, "secrethunt/secret", secretRequest, HttpUtils.RequestBehavior.NonCriticalBehavior, cancel, JsonOptions).ConfigureAwait(false);
			if (response is not null)
			{
				foreach (var match in response.Matches)
				{
					if (results.TryGetValue(match.Key, out var result))
					{
						result.AddMatchingTransaction(transaction, match.Value);
					}
				}
			}
		}
		catch
		{
		}
		results.ForEach(x => x.Value.AddCheckedTransaction(transaction));
	}

	private Dictionary<string, SecretHuntEventResult> CollectResults(List<Wallet> wallets)
	{
		Dictionary<string, SecretHuntEventResult> results = Events.Select(x => (x.Id, new SecretHuntEventResult(x))).ToDictionary();
		foreach (var wallet in wallets)
		{
			var walletResults = wallet.KeyManager.Attributes.SecretHuntResults;
			foreach (var walletResult in walletResults.Results)
			{
				// We are interested only in the active events
				if (results.TryGetValue(walletResult.Event.Id, out SecretHuntEventResult? result))
				{
					foreach (var secret in walletResult.Secrets)
					{
						if (!result.Secrets.TryGetValue(secret.Key, out List<NBitcoin.uint256>? coinjoinIds))
						{
							result.Secrets.Add(secret.Key, coinjoinIds = []);
						}
						secret.Value.Where(x => !coinjoinIds.Contains(x)).ForEach(x => coinjoinIds.Add(x));
					}
					foreach (var cjId in walletResult.CheckedWithNoResults)
					{
						if (!result.CheckedWithNoResults.Contains(cjId))
						{
							result.CheckedWithNoResults.Add(cjId);
						}
					}
					if (string.IsNullOrEmpty(result.ExtraSecret) && !string.IsNullOrEmpty(walletResult.ExtraSecret))
					{
						result.ExtraSecret = walletResult.ExtraSecret;
					}
				}
			}
		}
		return results;
	}
}
