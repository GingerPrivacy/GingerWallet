using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class CoinVerifierTests
{
	private WabiSabiConfig _wabisabiTestConfig = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();

	private CoinVerifierConfig _verifierConfig = new("cvp1", "https://test.com/test/", "key", "secret", "11");

	private int _mockBlockchainHeight = 733947; // Same as the example JSON report block height, otherwise we kick out the coin.

	private static string GoodResponse = """{"ban":false}""";
	private static string BanResponse = """{"ban":true}""";

	[Fact]
	public async Task DisposeWaitsForVerificationAndFlushesItsAuditAsync()
	{
		string auditDirectory = TestDirectory.Get();
		using MockHttpClient httpClient = new();
		using GatedCoinVerifierProvider provider = new(httpClient, _verifierConfig);
		await using CoinVerifierApiClient apiClient = new(httpClient, provider);
		await using CoinVerifierLogger audit = new(auditDirectory);
		await using CoinVerifier verifier = new(new CoinJoinIdStore(), apiClient, _wabisabiTestConfig, auditArchiver: audit);
		using CancellationTokenSource cancellation = new();
		Coin coin = GenerateCoins(1).Single();
		Task? disposal = null;
		try
		{
			Assert.True(verifier.TryScheduleVerification(coin, TimeSpan.Zero, _wabisabiTestConfig.CoinVerifierRequiredConfirmations, false, _mockBlockchainHeight, cancellation.Token));
			await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
			disposal = verifier.DisposeAsync().AsTask();
			Assert.False(disposal.IsCompleted);
			await provider.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
			Assert.False(verifier.TryScheduleVerification(GenerateCoins(1).Single(), TimeSpan.Zero, 1, true, _mockBlockchainHeight, CancellationToken.None));
			provider.AllowCompletion.TrySetResult();
			await disposal.WaitAsync(TimeSpan.FromSeconds(5));

			string auditFile = Assert.Single(Directory.GetFiles(auditDirectory, "VerifierAudits.*.txt"));
			Assert.Contains(coin.Outpoint.ToString(), await File.ReadAllTextAsync(auditFile));
		}
		finally
		{
			cancellation.Cancel();
			provider.AllowCompletion.TrySetResult();
			await provider.Finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
			if (disposal is not null)
			{
				await disposal.WaitAsync(TimeSpan.FromSeconds(5));
			}
		}
	}

	[Fact]
	public async Task CanceledVerificationCompletesWhenDisposedAsync()
	{
		using MockHttpClient httpClient = new();
		using MockCoinVerifierProvider provider = new(httpClient, _verifierConfig);
		await using CoinVerifierApiClient apiClient = new(httpClient, provider);
		await using CoinVerifierLogger audit = new(TestDirectory.Get());
		await using CoinVerifier verifier = new(new CoinJoinIdStore(), apiClient, _wabisabiTestConfig, auditArchiver: audit);
		Coin coin = GenerateCoins(1).Single();
		Assert.True(verifier.TryScheduleVerification(coin, TimeSpan.FromHours(1), _wabisabiTestConfig.CoinVerifierRequiredConfirmations, false, _mockBlockchainHeight, new CancellationToken(canceled: true)));
		var results = verifier.VerifyCoinsAsync([coin], CancellationToken.None);
		await verifier.DisposeAsync();
		Assert.True(Assert.Single(await results.WaitAsync(TimeSpan.FromSeconds(5))).ShouldRemove);
	}

	private sealed class GatedCoinVerifierProvider(HttpClient httpClient, CoinVerifierConfig config)
		: MockCoinVerifierProvider(httpClient, config)
	{
		public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource AllowCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public override async Task<ApiResponse> SendRequestAsync(Coin coin, int coinBlockHeight, int currentBlockHeight, CancellationToken cancellationToken)
		{
			Started.TrySetResult();
			try
			{
				await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
				throw new InvalidOperationException("The request should be cancelled.");
			}
			finally
			{
				CancellationObserved.TrySetResult();
				await AllowCompletion.Task.ConfigureAwait(false);
				Finished.TrySetResult();
			}
		}
	}

	public class MockCoinVerifierProvider : CoinVerifierProvider
	{
		public MockCoinVerifierProvider(HttpClient httpClient, CoinVerifierConfig config, string responseLogPath = "") : base(httpClient, config, responseLogPath)
		{
		}

		public override HttpRequestMessage CreateRequest(Coin coin)
		{
			return new HttpRequestMessage();
		}

		public override bool IsValid(HttpResponseMessage response)
		{
			return true;
		}

		public override ApiResponse ParseResponse(HttpStatusCode statusCode, string responseString, Coin coin, int coinBlockHeight, int currentBlockHeight)
		{
			bool ban = responseString == BanResponse;
			return new ApiResponse(ApiResponseInfo.OK, "mock", ban, ban, "", TimeSpan.Zero);
		}
	}

	[Fact]
	public async Task CanHandleBlacklistedUtxosTestAsync()
	{
		using MockHttpClient mockHttpClient = new();
		mockHttpClient.OnSendAsync = req =>
		{
			string content = BanResponse;
			HttpResponseMessage response = new(HttpStatusCode.OK);
			response.Content = new StringContent(content);
			return Task.FromResult(response);
		};
		using MockCoinVerifierProvider mockProvider = new(mockHttpClient, _verifierConfig);

		CoinJoinIdStore coinJoinIdStore = new();

		await using CoinVerifierApiClient apiClient = new(mockHttpClient, mockProvider);
		await using CoinVerifier coinVerifier = new(coinJoinIdStore, apiClient, _wabisabiTestConfig);

		List<Coin> generatedCoins = GenerateCoins(98);
		List<Coin> naughtyCoins = new();

		ScheduleVerifications(coinVerifier, generatedCoins);
		foreach (var item in await coinVerifier.VerifyCoinsAsync(generatedCoins, CancellationToken.None))
		{
			if (item.ShouldBan)
			{
				naughtyCoins.Add(item.Coin);
			}
		}

		Assert.Equal(98, naughtyCoins.Count);
	}

	[Fact]
	public async Task CanFilterNaughtyUtxoTestAsync()
	{
		using HttpResponseMessage dirtyResponse = new(HttpStatusCode.OK) { Content = new StringContent(BanResponse) };
		using HttpResponseMessage cleanResponse = new(HttpStatusCode.OK) { Content = new StringContent(GoodResponse) };

		using MockHttpClient mockHttpClient = new();
		mockHttpClient.SetupSequence(
			() => dirtyResponse,
			() => cleanResponse,
			() => cleanResponse,
			() => throw new InvalidOperationException(),
			() => throw new InvalidOperationException(), // Because of the retry mechanism, we need to fail 3 times to kick out the coin.
			() => throw new InvalidOperationException(),
			() => cleanResponse,
			() => cleanResponse,
			() => cleanResponse,
			() => cleanResponse,
			() => cleanResponse,
			() => cleanResponse);
		using MockCoinVerifierProvider mockProvider = new(mockHttpClient, _verifierConfig);

		List<Coin> naughtyCoins = new();
		CoinJoinIdStore coinJoinIdStore = new();
		await using CoinVerifierApiClient apiClient = new(mockHttpClient, mockProvider);
		await using CoinVerifier coinVerifier = new(coinJoinIdStore, apiClient, _wabisabiTestConfig);

		List<Coin> generatedCoins = GenerateCoins(10);
		List<Coin> removedCoins = new();
		List<Coin> checkedCoins = new();

		ScheduleVerifications(coinVerifier, generatedCoins);
		coinVerifier.CancelSchedule(generatedCoins[9]);

		foreach (var item in await coinVerifier.VerifyCoinsAsync(generatedCoins, CancellationToken.None))
		{
			checkedCoins.Add(item.Coin);
			if (item.ShouldBan)
			{
				naughtyCoins.Add(item.Coin);
			}
			if (item.ShouldRemove)
			{
				removedCoins.Add(item.Coin);
			}
		}

		Assert.Equal(10, checkedCoins.Count);
		Assert.Equal(2, removedCoins.Count);
		Assert.Single(naughtyCoins);
	}

	[Fact]
	public async Task HandleAuthenticationErrorTestAsync()
	{
		using MockHttpClient mockHttpClient = new();
		mockHttpClient.OnSendAsync = req =>
		{
			string content = """{"error": "User roles access forbidden." }""";
			HttpResponseMessage response = new(HttpStatusCode.Forbidden);
			response.Content = new StringContent(content);
			return Task.FromResult(response);
		};
		using MockCoinVerifierProvider mockProvider = new(mockHttpClient, _verifierConfig);

		List<Coin> naughtyCoins = new();
		CoinJoinIdStore coinJoinIdStore = new();
		await using CoinVerifierApiClient apiClient = new(mockHttpClient, mockProvider);
		await using CoinVerifier coinVerifier = new(coinJoinIdStore, apiClient, _wabisabiTestConfig);

		List<Coin> generatedCoins = GenerateCoins(5);

		ScheduleVerifications(coinVerifier, generatedCoins);
		foreach (var item in await coinVerifier.VerifyCoinsAsync(generatedCoins, CancellationToken.None))
		{
			if (item.ShouldBan)
			{
				naughtyCoins.Add(item.Coin);
			}
		}

		Assert.Empty(naughtyCoins); // Empty, so we won't kick out anyone from the CJ round.
	}

	[Fact]
	public async Task CanHandleAddressReuseAsync()
	{
		using var key = new Key();
		var generatedCoins = new[] {
			WabiSabiTestFactory.CreateCoin(key, Money.Coins(2m)),
			WabiSabiTestFactory.CreateCoin(key, Money.Coins(1m))
		};

		using MockHttpClient mockHttpClient = new();
		mockHttpClient.OnSendAsync = req =>
		{
			string content = GoodResponse;
			HttpResponseMessage response = new(HttpStatusCode.OK);
			response.Content = new StringContent(content);
			return Task.FromResult(response);
		};
		using MockCoinVerifierProvider mockProvider = new(mockHttpClient, _verifierConfig);

		CoinJoinIdStore coinJoinIdStore = new();
		await using CoinVerifierApiClient apiClient = new(mockHttpClient, mockProvider);
		await using CoinVerifier coinVerifier = new(coinJoinIdStore, apiClient, _wabisabiTestConfig);

		ScheduleVerifications(coinVerifier, generatedCoins);
		foreach (var item in await coinVerifier.VerifyCoinsAsync(generatedCoins, CancellationToken.None))
		{
			Assert.False(item.ShouldBan);
		}
	}

	[Fact]
	public async Task CanFillWhitelistAfterVerificationTestAsync()
	{
		using MockHttpClient mockHttpClient = new();
		mockHttpClient.OnSendAsync = req =>
		{
			string content = GoodResponse;
			HttpResponseMessage response = new(HttpStatusCode.OK);
			response.Content = new StringContent(content);
			return Task.FromResult(response);
		};
		using MockCoinVerifierProvider mockProvider = new(mockHttpClient, _verifierConfig);

		List<Coin> naughtyCoins = new();
		CoinJoinIdStore coinJoinIdStore = new();
		await using CoinVerifierApiClient apiClient = new(mockHttpClient, mockProvider);
		Whitelist whitelist = new(Enumerable.Empty<Innocent>(), string.Empty, WabiSabiTestFactory.CreateDefaultWabiSabiConfig());
		await using CoinVerifier coinVerifier = new(coinJoinIdStore, apiClient, _wabisabiTestConfig, whitelist);

		List<Coin> generatedCoins = GenerateCoins(10);

		ScheduleVerifications(coinVerifier, generatedCoins);

		foreach (CoinVerifyResult result in await coinVerifier.VerifyCoinsAsync(generatedCoins, CancellationToken.None))
		{
			if (result.ShouldBan)
			{
				naughtyCoins.Add(result.Coin);
			}
		}

		Assert.Empty(naughtyCoins); // Empty, so we won't kick out anyone from the CJ round.
		Assert.Equal(10, whitelist.CountInnocents());
	}

	private List<Coin> GenerateCoins(int numToGen)
	{
		List<Coin> coins = new();
		for (int i = 0; i < numToGen; i++)
		{
			using Key key = new();
			coins.Add(WabiSabiTestFactory.CreateCoin(key));
		}

		return coins;
	}

	private void ScheduleVerifications(CoinVerifier coinVerifier, IEnumerable<Coin> coins)
	{
		foreach (Coin coin in coins)
		{
			coinVerifier.TryScheduleVerification(coin, delayedStart: TimeSpan.Zero, confirmations: _wabisabiTestConfig.CoinVerifierRequiredConfirmations, oneHop: false, currentBlockHeight: _mockBlockchainHeight, CancellationToken.None);
		}
	}
}
