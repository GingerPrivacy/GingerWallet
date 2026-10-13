using GingerCommon.Crypto.Random;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.WabiSabi.Models.Serialization;
using WalletWasabi.Wallets;
using Xunit;
using Xunit.Abstractions;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration;

/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class WabiSabiHttpApiIntegrationTests : IAsyncLifetime
{
	private readonly WabiSabiApiApplicationFactory<Startup> _apiApplicationFactory = new();
	private readonly ITestOutputHelper _output;

	public WabiSabiHttpApiIntegrationTests(ITestOutputHelper output)
	{
		_output = output;
	}

	public Task InitializeAsync() => Task.CompletedTask;

	public Task DisposeAsync() => _apiApplicationFactory.DisposeAsync().AsTask();

	[Fact]
	public async Task CreateArenaClientWaitsForRoundAsync()
	{
		var round = RoundState.FromRound(WabiSabiTestFactory.CreateRound(cfg: WabiSabiTestFactory.CreateDefaultWabiSabiConfig()));
		var responses = new[]
		{
			new RoundStateResponse([], []), // Arena has started but has not published its first round yet.
			new RoundStateResponse([round], [])
		};
		var requestCount = 0;
		var httpClient = new MockIHttpClient
		{
			OnSendAsync = _ => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
			{
				Content = new StringContent(JsonConvert.SerializeObject(responses[requestCount++], JsonSerializationOptions.Default.Settings))
			})
		};

		var apiClient = await _apiApplicationFactory.CreateArenaClientAsync(httpClient);

		Assert.Equal(2, requestCount);
		Assert.Equal(round.CoinjoinState.Parameters.CoordinationIdentifier, apiClient.CoordinatorIdentifier);
	}

	[Fact]
	public async Task RegisterSpentOrInNonExistentCoinAsync()
	{
		var httpClient = _apiApplicationFactory.CreateClient();

		var apiClient = await _apiApplicationFactory.CreateArenaClientAsync(httpClient);
		var rounds = (await apiClient.GetStatusAsync(RoundStateRequest.Empty, CancellationToken.None)).RoundStates;
		var round = rounds.First(x => x.CoinjoinState is ConstructionState);

		// If an output is not in the utxo dataset then it is not unspent, this
		// means that the output is spent or simply doesn't even exist.
		var nonExistingOutPoint = new OutPoint();
		using var signingKey = new Key();
		var ownershipProof = WabiSabiTestFactory.CreateOwnershipProof(TestRandom.Get(), signingKey, round.Id);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () =>
		   await apiClient.RegisterInputAsync(round.Id, nonExistingOutPoint, ownershipProof, CancellationToken.None));

		Assert.Equal(WabiSabiProtocolErrorCode.InputSpent, ex.ErrorCode);
	}

	[Fact]
	public async Task RegisterBannedCoinAsync()
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

		using var signingKey = new Key();
		var coin = WabiSabiTestFactory.CreateCoin(signingKey);
		var bannedOutPoint = coin.Outpoint;

		var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			builder.ConfigureServices(services =>
			{
				var rpc = BitcoinFactory.GetMockMinimalRpc();

				// Make the coordinator believe that the coins are real and
				// that they exist in the blockchain with many confirmations.
				rpc.OnGetTxOutAsync = (_, _, _) => new()
				{
					Confirmations = 101,
					IsCoinBase = false,
					ScriptPubKeyType = "witness_v0_keyhash",
					TxOut = coin.TxOut
				};
				services.AddScoped<IRPCClient>(s => rpc);

				var prison = WabiSabiTestFactory.CreatePrison();
				prison.FailedVerification(bannedOutPoint, uint256.One, TimeSpan.Zero, "");
				services.AddScoped(_ => prison);
			})).CreateClient();

		var apiClient = await _apiApplicationFactory.CreateArenaClientAsync(httpClient);
		var rounds = (await apiClient.GetStatusAsync(RoundStateRequest.Empty, timeoutCts.Token)).RoundStates;
		var round = rounds.First(x => x.CoinjoinState is ConstructionState);

		// If an output is not in the utxo dataset then it is not unspent, this
		// means that the output is spent or simply doesn't even exist.
		var ownershipProof = WabiSabiTestFactory.CreateOwnershipProof(TestRandom.Get(), signingKey, round.Id);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () =>
			await apiClient.RegisterInputAsync(round.Id, bannedOutPoint, ownershipProof, timeoutCts.Token));

		Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);
		var inputBannedData = Assert.IsType<InputBannedExceptionData>(ex.ExceptionData);
		Assert.True(inputBannedData.BannedUntil > DateTimeOffset.UtcNow);
	}

	[Theory]
	[InlineData(new long[] { 10_000_000, 20_000_000, 30_000_000, 40_000_000, 100_000_000 })]
	public async Task SoloCoinJoinTestAsync(long[] amounts)
	{
		int inputCount = amounts.Length;

		// At the end of the test a coinjoin transaction has to be created and broadcasted.
		var transactionCompleted = new TaskCompletionSource<Transaction>();

		// Create a key manager and use it to create fake coins.
		_output.WriteLine("Creating key manager...");
		KeyManager keyManager = KeyManager.CreateNew(out _, password: "", Network.Main);

		var coins = GenerateSmartCoins(TestRandom.Get(), keyManager, amounts, inputCount);

		_output.WriteLine("Coins were created successfully");

		var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			builder.AddMockRpcClient(
				coins,
				rpc =>

					// Make the coordinator believe that the transaction is being
					// broadcasted using the RPC interface. Once we receive this tx
					// (the `SendRawTransactionAsync` was invoked) we stop waiting
					// and finish the waiting tasks to finish the test successfully.
					rpc.OnSendRawTransactionAsync = (tx) =>
					{
						transactionCompleted.SetResult(tx);
						return tx.GetHash();
					})
			.ConfigureServices(services =>
			{
				// Instruct the coordinator DI container to use these two scoped
				// services to build everything (WabiSabi controller, arena, etc)
				services.AddScoped(s =>
				{
					WabiSabiConfig config = WabiSabiApiApplicationFactory<Startup>.CreateConfig(inputCount - 1); // Make sure that at least one IR fails for WrongPhase
					config.StandardInputRegistrationTimeout = TimeSpan.FromSeconds(40);
					config.BlameInputRegistrationTimeout = TimeSpan.FromSeconds(40);
					config.ConnectionConfirmationTimeout = TimeSpan.FromSeconds(40);
					config.OutputRegistrationTimeout = TimeSpan.FromSeconds(40);
					config.TransactionSigningTimeout = TimeSpan.FromSeconds(40);
					config.FailFastOutputRegistrationTimeout = TimeSpan.FromMinutes(3);
					config.FailFastTransactionSigningTimeout = TimeSpan.FromMinutes(1);
					return config;
				});

				// Emulate that the first coin is coming from a coinjoin.
				services.AddScoped(s => new InMemoryCoinJoinIdStore(new[] { coins[0].Coin.Outpoint.Hash }));
			})).CreateClient();

		// Create the coinjoin client
		using PersonCircuit personCircuit = new();
		IHttpClient httpClientWrapper = new ClearnetHttpClient(httpClient);
		var apiClient = _apiApplicationFactory.CreateWabiSabiHttpApiClient(httpClient);

		var mockHttpClientFactory = new MockWasabiHttpClientFactory();

		mockHttpClientFactory.OnNewHttpClientWithPersonCircuit = () => (personCircuit, httpClientWrapper);
		mockHttpClientFactory.OnNewHttpClientWithCircuitPerRequest = () => httpClientWrapper;

		// Total test timeout.
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(200));
		cts.Token.Register(() => transactionCompleted.TrySetCanceled(), useSynchronizationContext: false);

		using var roundStateUpdater = new RoundStateUpdater(WabiSabiIntegrationTestConstants.RequestInterval, ["CoinJoinCoordinatorIdentifier"], apiClient, false);

		await roundStateUpdater.StartAsync(CancellationToken.None);

		var coinJoinClient = WabiSabiTestFactory.CreateTestCoinJoinClient(mockHttpClientFactory, keyManager, roundStateUpdater);

		// Run the coinjoin client task.
		var coinjoinResult = await coinJoinClient.StartCoinJoinAsync(async () => await Task.FromResult(coins), true, cts.Token);
		Assert.True(coinjoinResult is SuccessfulCoinJoinResult);

		var broadcastedTx = await transactionCompleted.Task; // wait for the transaction to be broadcasted.
		Assert.NotNull(broadcastedTx);

		await roundStateUpdater.StopAsync(CancellationToken.None);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task ErrorWhileRegisterOutputsCoinJoinTestAsync(bool signalReadyToSign)
	{
		long[] amounts = new long[] { 10_000_000, 20_000_000, 30_000_000 };
		int inputCount = amounts.Length;

		// Create a key manager and use it to create fake coins.
		_output.WriteLine("Creating key manager...");
		KeyManager keyManager = KeyManager.CreateNew(out var _, password: "", Network.Main);

		var coins = GenerateSmartCoins(TestRandom.Get(), keyManager, amounts, inputCount);

		_output.WriteLine("Coins were created successfully");

		keyManager.AssertLockedInternalKeysIndexed(14, false);
		keyManager.AssertLockedInternalKeysIndexed(14, true);
		var outputScriptCandidates = keyManager
			.GetKeys(x => x.IsInternal && x.KeyState == KeyState.Locked)
			.Select(x => x.GetAddress(Network.Main).ScriptPubKey)
			.ToImmutableArray();

		var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			builder
			.AddMockRpcClient(coins, rpc => { })
			.ConfigureServices(services =>
			{
				// Instruct the coordinator DI container to use this scoped
				// services to build everything (WabiSabi controller, arena, etc)
				services.AddScoped(s => WabiSabiApiApplicationFactory<Startup>.CreateConfig(inputCount));

				// Emulate that all our outputs had been already used in the past.
				// the server will prevent the registration and fail with a WabiSabiProtocolError.
				services.AddScoped(s => new CoinJoinScriptStore(outputScriptCandidates));
			})).CreateClient();

		// Create the coinjoin client
		using PersonCircuit personCircuit = new();
		var rejectedOutputRounds = new ConcurrentQueue<uint256>();
		var recordingHttpClient = new Mock<ClearnetHttpClient>(httpClient);
		recordingHttpClient.Setup(client => client.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
			.Returns(async (HttpRequestMessage request, CancellationToken cancellationToken) =>
			{
				if (!signalReadyToSign && request.RequestUri!.AbsolutePath.EndsWith("/ready-to-sign", StringComparison.Ordinal))
				{
					// Exercise the abort path when no Alice signals readiness before the phase expires.
					return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
				}

				// TestServer consumes the request content; capture the round before sending it.
				var registration = request.RequestUri!.AbsolutePath.EndsWith("/output-registration", StringComparison.Ordinal)
					? JsonConvert.DeserializeObject<OutputRegistrationRequest>(await request.Content!.ReadAsStringAsync(cancellationToken), JsonSerializationOptions.Default.Settings)
					: null;
				var response = await httpClient.SendAsync(request, cancellationToken);
				if (!response.IsSuccessStatusCode && registration is not null)
				{
					var error = JsonConvert.DeserializeObject<Error>(await response.Content.ReadAsStringAsync(cancellationToken), JsonSerializationOptions.Default.Settings);
					if (error is { Type: ProtocolConstants.ProtocolViolationType, ErrorCode: nameof(WabiSabiProtocolErrorCode.AlreadyRegisteredScript) })
					{
						rejectedOutputRounds.Enqueue(registration.RoundId);
					}
				}

				return response;
			});
		IHttpClient httpClientWrapper = recordingHttpClient.Object;
		var apiClient = _apiApplicationFactory.CreateWabiSabiHttpApiClient(httpClient);
		var mockHttpClientFactory = new MockWasabiHttpClientFactory();
		mockHttpClientFactory.OnNewHttpClientWithPersonCircuit = () => (personCircuit, httpClientWrapper);
		mockHttpClientFactory.OnNewHttpClientWithCircuitPerRequest = () => httpClientWrapper;

		// Total test timeout.
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(200));

		using var roundStateUpdater = new RoundStateUpdater(WabiSabiIntegrationTestConstants.RequestInterval, ["CoinJoinCoordinatorIdentifier"], apiClient, false);

		await roundStateUpdater.StartAsync(CancellationToken.None);

		// Exercise output rejection with one predetermined output per input instead of a random decomposition graph.
		var outputProvider = new Mock<OutputProvider>(new InternalDestinationProvider(keyManager), null!);
		outputProvider.Setup(provider => provider.GetOutputs(
			It.IsAny<uint256>(), It.IsAny<RoundParameters>(), It.IsAny<IEnumerable<Money>>(),
			It.IsAny<List<Money>>(), It.IsAny<List<double>>(), It.IsAny<int>()))
			.Returns((uint256 _, RoundParameters parameters, IEnumerable<Money> values, List<Money> denoms, List<double> frequencies, int vsize) =>
				values.Zip(outputScriptCandidates, (value, script) => new TxOut(value - parameters.MiningFeeRate.GetFee(script.EstimateOutputVsize()), script)));
		var coinJoinClient = WabiSabiTestFactory.CreateTestCoinJoinClient(
			mockHttpClientFactory, new KeyChain(keyManager, new Kitchen("")), outputProvider.Object, roundStateUpdater, keyManager.RedCoinIsolation);

		RoundState? endedRound = null;
		void HandleCoinJoinProgress(object? sender, CoinJoinProgressEventArgs coinJoinProgress)
		{
			if (coinJoinProgress is RoundEnded roundEnded)
			{
				endedRound = roundEnded.LastRoundState;
				cts.Cancel(); // this is what we were waiting for so, end the test.
			}
		}

		try
		{
			coinJoinClient.CoinJoinClientProgress += HandleCoinJoinProgress;

			// Run the coinjoin client task.
			var coinjoinResult = await coinJoinClient.StartCoinJoinAsync(async () => await Task.FromResult(coins), true, cts.Token);
			if (coinjoinResult is SuccessfulCoinJoinResult)
			{
				throw new Exception("Coinjoin should have never finished successfully.");
			}
		}
		catch (OperationCanceledException) when (endedRound is not null)
		{
			// The progress handler stops the client once the round ends.
		}
		finally
		{
			coinJoinClient.CoinJoinClientProgress -= HandleCoinJoinProgress;
			await roundStateUpdater.StopAsync(CancellationToken.None);
		}

		// Check both completion paths: an unrelated failed round must not pass this test.
		Assert.NotNull(endedRound);
		Assert.Contains(endedRound.Id, rejectedOutputRounds);
		// Readiness timing determines whether enough Alices remain to start a blame round.
		Assert.Contains(endedRound.EndRoundState, new[] { EndRoundState.NotAllAlicesSign, EndRoundState.AbortedNotEnoughAlicesSigned });
		if (!signalReadyToSign)
		{
			Assert.Equal(EndRoundState.AbortedNotEnoughAlicesSigned, endedRound.EndRoundState);
		}
	}

	[Theory]
	[InlineData(new long[] { 20_000_000, 40_000_000, 60_000_000, 80_000_000 })]
	public async Task CoinJoinWithBlameRoundTestAsync(long[] amounts)
	{
		var rnd = TestRandom.Get();
		int inputCount = amounts.Length;

		// At the end of the test a coinjoin transaction has to be created and broadcasted.
		var transactionCompleted = new TaskCompletionSource<Transaction>(TaskCreationOptions.RunContinuationsAsynchronously);

		// Total test timeout.
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		cts.Token.Register(() => transactionCompleted.TrySetCanceled(), useSynchronizationContext: false);

		KeyManager keyManager1 = KeyManager.CreateNew(out var _, password: "", Network.Main);
		KeyManager keyManager2 = KeyManager.CreateNew(out var _, password: "", Network.Main);

		var coins = GenerateSmartCoins(rnd, keyManager1, amounts, inputCount);
		var badCoins = GenerateSmartCoins(rnd, keyManager2, amounts, inputCount);

		var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
		builder.AddMockRpcClient(
			Enumerable.Concat(coins, badCoins).ToArray(),
			rpc =>
			{
				rpc.OnGetRawTransactionAsync = (txid, throwIfNotFound) =>
				{
					var tx = Transaction.Create(Network.Main);
					return Task.FromResult(tx);
				};

				// Make the coordinator believe that the transaction is being
				// broadcasted using the RPC interface. Once we receive this tx
				// (the `SendRawTransactionAsync` was invoked) we stop waiting
				// and finish the waiting tasks to finish the test successfully.
				rpc.OnSendRawTransactionAsync = (tx) =>
				{
					transactionCompleted.SetResult(tx);
					return tx.GetHash();
				};
			})
		.ConfigureServices(services =>

			// Instruct the coordinator DI container to use this scoped
			// services to build everything (WabiSabi controller, arena, etc)
			services.AddScoped(s =>
			{
				WabiSabiConfig config = WabiSabiApiApplicationFactory<Startup>.CreateConfig(2 * inputCount);
				config.AllowP2trInputs = true;
				config.AllowP2trOutputs = true;
				config.StandardInputRegistrationTimeout = TimeSpan.FromSeconds(40);
				config.ConnectionConfirmationTimeout = TimeSpan.FromSeconds(40);
				config.OutputRegistrationTimeout = TimeSpan.FromSeconds(40);
				config.TransactionSigningTimeout = TimeSpan.FromSeconds(20);
				config.BlameInputRegistrationTimeout = TimeSpan.FromSeconds(20);
				config.FailFastOutputRegistrationTimeout = TimeSpan.FromMinutes(3);
				config.FailFastTransactionSigningTimeout = TimeSpan.FromMinutes(1);
				return config;
			}))).CreateClient();

		// Create the coinjoin client
		using PersonCircuit personCircuit = new();
		IHttpClient httpClientWrapper = new ClearnetHttpClient(httpClient);

		var apiClient = _apiApplicationFactory.CreateWabiSabiHttpApiClient(httpClient);
		var mockHttpClientFactory = new MockWasabiHttpClientFactory();
		mockHttpClientFactory.OnNewHttpClientWithPersonCircuit = () => (personCircuit, httpClientWrapper);
		mockHttpClientFactory.OnNewHttpClientWithCircuitPerRequest = () => httpClientWrapper;

		// Acknowledge signatures without sending them, so this client forces a blame round.
		var nonSigningHttpClientMock = new MockIHttpClient();
		nonSigningHttpClientMock.OnSendAsync = req =>
		{
			if (req.RequestUri!.AbsolutePath.Contains("transaction-signature"))
			{
				return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
			}

			return httpClient.SendAsync(req, CancellationToken.None);
		};

		IHttpClient nonSigningHttpClient = nonSigningHttpClientMock;
		var mockNonSigningHttpClientFactory = new MockWasabiHttpClientFactory();
		mockNonSigningHttpClientFactory.OnNewHttpClientWithPersonCircuit = () => (personCircuit, nonSigningHttpClient);
		mockNonSigningHttpClientFactory.OnNewHttpClientWithCircuitPerRequest = () => nonSigningHttpClient;

		using var roundStateUpdater = new RoundStateUpdater(WabiSabiIntegrationTestConstants.RequestInterval, [], apiClient, false);
		await roundStateUpdater.StartAsync(CancellationToken.None);

		var coinJoinClient = WabiSabiTestFactory.CreateTestCoinJoinClient(mockHttpClientFactory, keyManager1, roundStateUpdater);
		var badCoinJoinClient = WabiSabiTestFactory.CreateTestCoinJoinClient(mockNonSigningHttpClientFactory, keyManager2, roundStateUpdater);
		var initialRound = new TaskCompletionSource<RoundState>(TaskCreationOptions.RunContinuationsAsynchronously);
		var completedRounds = new ConcurrentQueue<RoundState>();
		void HandleCoinJoinProgress(object? sender, CoinJoinProgressEventArgs progress)
		{
			if (progress is EnteringInputRegistrationPhase entering)
			{
				initialRound.TrySetResult(entering.RoundState);
			}
			else if (progress is RoundEnded ended)
			{
				completedRounds.Enqueue(ended.LastRoundState);
			}
		}
		coinJoinClient.CoinJoinClientProgress += HandleCoinJoinProgress;

		// Run the coinjoin client task.
		var coinJoinTask = coinJoinClient.StartCoinJoinAsync(async () => await Task.FromResult(coins), true, cts.Token);
		Task<CoinJoinResult>? badCoinsTask = null;

		try
		{
			// Both clients must join the round selected by the honest client.
			var roundState = await initialRound.Task.WaitAsync(cts.Token);
			badCoinsTask = badCoinJoinClient.StartRoundAsync(badCoins, roundState, cts.Token);
			var resultBad = await badCoinsTask;
			// The mock acknowledges signatures without forwarding them to the coordinator.
			// Its local result can be disrupted or failed, but it must never succeed.
			Assert.IsNotType<SuccessfulCoinJoinResult>(resultBad);

			var resultOk = await coinJoinTask;

			Assert.IsType<SuccessfulCoinJoinResult>(resultOk);
			Assert.Collection(completedRounds,
				initial =>
				{
					Assert.Equal(roundState.Id, initial.Id);
					Assert.Equal(EndRoundState.NotAllAlicesSign, initial.EndRoundState);
				},
				blame =>
				{
					Assert.Equal(roundState.Id, blame.BlameOf);
					Assert.Equal(EndRoundState.TransactionBroadcasted, blame.EndRoundState);
				});

			var broadcastedTx = await transactionCompleted.Task; // wait for the transaction to be broadcasted.
			Assert.NotNull(broadcastedTx);

			Assert.Equal(
				coins.Select(x => x.Coin.Outpoint.ToString()).OrderBy(x => x),
				broadcastedTx.Inputs.Select(x => x.PrevOut.ToString()).OrderBy(x => x));
		}
		finally
		{
			await cts.CancelAsync();
			// Observe unfinished tasks without hiding a failure from the assertions above.
			await Task.WhenAll(coinJoinTask, badCoinsTask ?? Task.CompletedTask).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
			coinJoinClient.CoinJoinClientProgress -= HandleCoinJoinProgress;
			await roundStateUpdater.StopAsync(CancellationToken.None);
		}
	}

	[Theory]
	[InlineData(123456, 0.00, 0.00)]
	public async Task MultiClientsCoinJoinTestAsync(
		int seed,
		double faultInjectorMonkeyAggressiveness,
		double delayInjectorMonkeyAggressiveness)
	{
		const int NumberOfParticipants = 10;
		const int NumberOfCoinsPerParticipant = 2;
		const int ExpectedInputNumber = (NumberOfParticipants * NumberOfCoinsPerParticipant) / 2;

		var node = await TestNodeBuilder.CreateForHeavyConcurrencyAsync();
		try
		{
			var rpc = new TestableRpcClient((RpcClientBase)node.RpcClient);

			TaskCompletionSource<Transaction> coinJoinBroadcasted = new();
			rpc.AfterSendRawTransaction = (tx) =>
			{
				if (tx.Inputs.Count > 1)
				{
					coinJoinBroadcasted.SetResult(tx);
				}
			};

			var app = _apiApplicationFactory.WithWebHostBuilder(builder =>
				builder.ConfigureServices(services =>
				{
					// Instruct the coordinator DI container to use these two scoped
					// services to build everything (WabiSabi controller, arena, etc)
					services.AddScoped<IRPCClient>(s => rpc);
					services.AddScoped(s =>
					{
						WabiSabiConfig config = WabiSabiBackendFactory.Instance.CreateWabiSabiConfig(Path.GetTempFileName());
						config.MaxRegistrableAmount = Money.Coins(500m);
						config.MaxInputCountByRound = (int)(ExpectedInputNumber / (1 + (10 * (faultInjectorMonkeyAggressiveness + delayInjectorMonkeyAggressiveness))));
						config.StandardInputRegistrationTimeout = TimeSpan.FromSeconds(5 * ExpectedInputNumber);
						config.BlameInputRegistrationTimeout = TimeSpan.FromSeconds(2 * ExpectedInputNumber);
						config.ConnectionConfirmationTimeout = TimeSpan.FromSeconds(2 * ExpectedInputNumber);
						config.OutputRegistrationTimeout = TimeSpan.FromSeconds(5 * ExpectedInputNumber);
						config.TransactionSigningTimeout = TimeSpan.FromSeconds(3 * ExpectedInputNumber);
						config.MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountCredentialValue);
						config.CreateNewRoundBeforeInputRegEnd = TimeSpan.Zero;
						return config;
					});
				}));

			using PersonCircuit personCircuit = new();
			IHttpClient httpClientWrapper = new MonkeyHttpClient(
				new ClearnetHttpClient(app.CreateClient()),
				() => // This monkey injects `HttpRequestException` randomly to simulate errors in the communication.
				{
					if (Random.Shared.NextDouble() < faultInjectorMonkeyAggressiveness)
					{
						throw new HttpRequestException("Crazy monkey hates you, donkey.");
					}
					return Task.CompletedTask;
				},
				async () => // This monkey injects `Delays` randomly to simulate slow response times.
				{
					double delay = Random.Shared.NextDouble();
					await Task.Delay(TimeSpan.FromSeconds(5 * delayInjectorMonkeyAggressiveness)).ConfigureAwait(false);
				});

			var mockHttpClientFactory = new MockWasabiHttpClientFactory();
			mockHttpClientFactory.OnNewHttpClientWithPersonCircuit = () => (personCircuit, httpClientWrapper);
			mockHttpClientFactory.OnNewHttpClientWithCircuitPerRequest = () => httpClientWrapper;
			mockHttpClientFactory.OnNewHttpClientWithDefaultCircuit = () => httpClientWrapper;

			// Total test timeout.
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

			var participants = Enumerable
				.Range(0, NumberOfParticipants)
				.Select(i => new Participant($"participant{i}", rpc, mockHttpClientFactory))
				.ToArray();

			foreach (var participant in participants)
			{
				await participant.GenerateSourceCoinAsync(cts.Token);
			}
			var dummyWallet = new TestWallet("dummy", rpc);
			await dummyWallet.GenerateAsync(101, cts.Token);
			foreach (var participant in participants)
			{
				await participant.GenerateCoinsAsync(NumberOfCoinsPerParticipant, seed, cts.Token);
			}
			await dummyWallet.GenerateAsync(101, cts.Token);

			using var participantCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
			var tasks = participants.Select(x => x.StartParticipatingAsync(participantCts.Token)).ToArray();

			try
			{
				var coinjoinTransactionCompletionTask = coinJoinBroadcasted.Task.WaitAsync(cts.Token);
				var participantsFinishedTask = Task.WhenAll(tasks);
				var finishedTask = await Task.WhenAny(participantsFinishedTask, coinjoinTransactionCompletionTask);
				if (finishedTask == coinjoinTransactionCompletionTask)
				{
					var broadcastedCoinjoinTransaction = await coinjoinTransactionCompletionTask;
					var mempool = await rpc.GetRawMempoolAsync();
					var coinjoinFromMempool = await rpc.GetRawTransactionAsync(mempool.Single());

					Assert.Equal(broadcastedCoinjoinTransaction.GetHash(), coinjoinFromMempool.GetHash());
				}
				else if (finishedTask == participantsFinishedTask)
				{
					var participantsFinishedSuccessully = tasks
						.Where(t => t.IsCompletedSuccessfully)
						.Select(t => t.Result)
						.ToArray();

					// In case some participants claim to have finished successfully then wait a second for seeing
					// the coinjoin in the mempool. This seems really hard to believe but just in case.
					if (participantsFinishedSuccessully.All(x => x is SuccessfulCoinJoinResult))
					{
						await Task.Delay(TimeSpan.FromSeconds(1));
						var mempool = await rpc.GetRawMempoolAsync();
						Assert.Single(mempool);
					}
					else if (participantsFinishedSuccessully.All(x => x is FailedCoinJoinResult))
					{
						throw new Exception("All participants finished, but CoinJoin still not in the mempool (no more blame rounds).");
					}
					else if (participantsFinishedSuccessully.Length == 0 && !cts.IsCancellationRequested)
					{
						var exceptions = tasks
							.Where(x => x.IsFaulted)
							.Select(x => new Exception("Something went wrong", x.Exception))
							.ToArray();
						throw new AggregateException(exceptions);
					}
					else
					{
						throw new Exception("All participants finished, but CoinJoin still not in the mempool.");
					}
				}
				else
				{
					throw new Exception("This is not so possible.");
				}
			}
			catch (OperationCanceledException)
			{
				throw new TimeoutException("Coinjoin was not propagated.");
			}
			finally
			{
				// Stop remaining clients before shutting down their coordinator and Bitcoin Core.
				await participantCts.CancelAsync();
				Task participantsFinishedTask = Task.WhenAll(tasks);
				await participantsFinishedTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
			}
		}
		finally
		{
			await node.TryStopAsync(true, 2);
		}
	}

	[Fact]
	public async Task RegisterCoinIdempotencyAsync()
	{
		var rnd = TestRandom.Get();
		using Key signingKey = new();
		Coin coinToRegister = new(
			fromOutpoint: BitcoinFactory.CreateOutPoint(rnd),
			fromTxOut: new TxOut(Money.Coins(1), signingKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit)));

		using HttpClient httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			builder.ConfigureServices(services =>
			{
				var rpc = BitcoinFactory.GetMockMinimalRpc();
				rpc.OnGetTxOutAsync = (_, _, _) => new()
				{
					Confirmations = 101,
					IsCoinBase = false,
					ScriptPubKeyType = "witness_v0_keyhash",
					TxOut = coinToRegister.TxOut
				};
				rpc.OnGetRawTransactionAsync = (txid, throwIfNotFound) =>
				{
					var tx = Transaction.Create(Network.Main);
					return Task.FromResult(tx);
				};
				services.AddScoped<IRPCClient>(s => rpc);
				services.AddScoped(_ => WabiSabiApiApplicationFactory<Startup>.CreateConfig(10));
			})).CreateClient();

		ArenaClient apiClient = await _apiApplicationFactory.CreateArenaClientAsync(httpClient);
		RoundState[] rounds = (await apiClient.GetStatusAsync(RoundStateRequest.Empty, CancellationToken.None)).RoundStates;
		RoundState round = rounds.First(x => x.CoinjoinState is ConstructionState);
		var stutteredHttpClient = new StuttererHttpClient(httpClient);
		var stutteredApiClient = new ArenaClient(
			apiClient.AmountCredentialClient,
			apiClient.VsizeCredentialClient,
			apiClient.CoordinatorIdentifier,
			new WabiSabiHttpApiClient(stutteredHttpClient));

		var ownershipProof = WabiSabiTestFactory.CreateOwnershipProof(rnd, signingKey, round.Id);
		var (response, _) = await stutteredApiClient.RegisterInputAsync(round.Id, coinToRegister.Outpoint, ownershipProof, CancellationToken.None);

		Assert.NotEqual(Guid.Empty, response.Value);
	}

	private SmartCoin[] GenerateSmartCoins(GingerRandom rnd, KeyManager keyManager, long[] amounts, int inputCount)
	{
		return keyManager.GetKeys()
			.Take(inputCount)
			.Select((x, i) => BitcoinFactory.CreateSmartCoin(rnd, x, Money.Satoshis(amounts[i])))
			.ToArray();
	}

	public class TestableRpcClient : RpcClientBase
	{
		public TestableRpcClient(RpcClientBase rpc)
			: base(rpc.Rpc)
		{
		}

		public Action<Transaction>? AfterSendRawTransaction { get; set; }

		public override async Task<uint256> SendRawTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default)
		{
			var ret = await base.SendRawTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
			AfterSendRawTransaction?.Invoke(transaction);
			return ret;
		}
	}
}
