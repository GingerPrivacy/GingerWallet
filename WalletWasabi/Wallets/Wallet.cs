using LinqKit;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Userfacing;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.Batching;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;

namespace WalletWasabi.Wallets;

public class Wallet : BackgroundService, IWallet
{
	private volatile WalletState _state;

	public Wallet(
		string dataDir,
		Network network,
		KeyManager keyManager,
		BitcoinStore bitcoinStore,
		WasabiSynchronizer syncer,
		ServiceConfiguration serviceConfiguration,
		IWalletFeeRateProvider feeProvider,
		TransactionProcessor transactionProcessor,
		WalletFilterProcessor walletFilterProcessor,
		UnconfirmedTransactionChainProvider unconfirmedTransactionChainProvider)
	{
		Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
		Network = network;
		KeyManager = keyManager;
		BitcoinStore = bitcoinStore;
		Synchronizer = syncer;
		ServiceConfiguration = serviceConfiguration;
		FeeProvider = feeProvider;
		UnconfirmedTransactionChainProvider = unconfirmedTransactionChainProvider;

		RuntimeParams.SetDataDir(dataDir);

		if (!KeyManager.IsWatchOnly)
		{
			KeyChain = new KeyChain(KeyManager, Kitchen);
		}

		DestinationProvider = new InternalDestinationProvider(KeyManager);

		TransactionProcessor = transactionProcessor;
		Coins = TransactionProcessor.Coins;
		WalletFilterProcessor = walletFilterProcessor;
		BatchedPayments = new PaymentBatch();
		OutputProvider = new PaymentAwareOutputProvider(DestinationProvider, BatchedPayments);
		WalletId = new WalletId(Guid.NewGuid());
	}

	public event EventHandler<ProcessedResult>? WalletRelevantTransactionProcessed;

	public event EventHandler<IEnumerable<FilterModel>>? NewFiltersProcessed;

	public event EventHandler<WalletState>? StateChanged;

	public WalletId WalletId { get; }

	public WalletState State
	{
		get => _state;
		private set
		{
			if (_state == value)
			{
				return;
			}

			_state = value;
			StateChanged?.Invoke(this, _state);
		}
	}

	public BitcoinStore BitcoinStore { get; }
	public KeyManager KeyManager { get; }
	public WasabiSynchronizer Synchronizer { get; }
	public ServiceConfiguration ServiceConfiguration { get; }
	public string WalletName => KeyManager.WalletName;

	public CoinsRegistry Coins { get; }

	public bool RedCoinIsolation => KeyManager.RedCoinIsolation;
	public CoinjoinSkipFactors CoinjoinSkipFactors => KeyManager.CoinjoinSkipFactors;

	public CoinJoinCoinSelectionSettings CoinJoinCoinSelectionSettings => KeyManager.Attributes.CoinJoinCoinSelectionSettings;

	public Network Network { get; }
	public TransactionProcessor TransactionProcessor { get; }

	public IWalletFeeRateProvider FeeProvider { get; }
	public UnconfirmedTransactionChainProvider UnconfirmedTransactionChainProvider { get; }
	public WalletFilterProcessor WalletFilterProcessor { get; }
	public FilterModel? LastProcessedFilter => WalletFilterProcessor.LastProcessedFilter;

	public bool IsLoggedIn { get; private set; }

	public Kitchen Kitchen { get; } = new();

	public IKeyChain? KeyChain { get; }

	public IDestinationProvider DestinationProvider { get; }

	public OutputProvider OutputProvider { get; }
	public PaymentBatch BatchedPayments { get; }

	public int AnonScoreTarget => KeyManager.AnonScoreTarget;
	public bool ConsolidationMode { get; set; }

	public bool IsMixable =>
		State == WalletState.Started // Only running wallets
		&& !KeyManager.IsWatchOnly // that are not watch-only wallets
		&& Kitchen.HasIngredients;

	public int SafeMiningFeeRate => KeyManager.SafeMiningFeeRate;

	public TimeSpan FeeRateMedianTimeFrame => TimeSpan.FromHours(KeyManager.FeeRateMedianTimeFrameHours);

	public bool IsUnderPlebStop => Coins.TotalAmount() <= KeyManager.PlebStopThreshold;

	public ICoinsView GetAllCoins() => Coins.AsAllCoinsView();

	public Task<bool> IsWalletPrivateAsync() => Task.FromResult(IsWalletPrivate());

	public bool IsWalletPrivate() => GetPrivacyPercentage() >= 100;

	public Task<IEnumerable<SmartTransaction>> GetTransactionsAsync() => Task.FromResult(GetTransactions());

	public Task<IEnumerable<SmartCoin>> GetCoinjoinCoinCandidatesAsync() => Task.FromResult(GetCoinjoinCoinCandidates());

	public IEnumerable<SmartCoin> GetCoinjoinCoinCandidates() => Coins;

	/// <summary>
	/// Get all the transactions associated to the wallet ordered by blockchain.
	/// </summary>
	public IEnumerable<SmartTransaction> GetTransactions()
	{
		var walletTransactions = new HashSet<SmartTransaction>();

		foreach (SmartCoin coin in GetAllCoins())
		{
			walletTransactions.Add(coin.Transaction);
			if (coin.SpenderTransaction is not null)
			{
				walletTransactions.Add(coin.SpenderTransaction);
			}
		}

		return walletTransactions.OrderByBlockchain().ToList();
	}

	/// <summary>
	/// Get all wallet transactions along with corresponding amounts ordered by blockchain.
	/// </summary>
	/// <param name="sortForUi"><c>true</c> to sort by "first seen", "height", and "block index", <c>false</c> to sort by "height", "block index", and "first seen".</param>
	/// <remarks>Transaction amount specifies how it affected your final wallet balance (spend some bitcoin, received some bitcoin, or no change).</remarks>
	public List<TransactionSummary> BuildHistorySummary(bool sortForUi = false)
	{
		Dictionary<uint256, TransactionSummary> mapByTxid = new();

		foreach (SmartCoin coin in GetAllCoins())
		{
			if (!mapByTxid.TryGetValue(coin.TransactionId, out TransactionSummary? incomeTransaction))
			{
				// Create if we don't have it yet
				var unconfTransactionChainOfCoin = UnconfirmedTransactionChainProvider.GetUnconfirmedTransactionChain(coin.TransactionId) ?? [];
				var effectiveFeeRate = FeeHelpers.CalculateEffectiveFeeRateOfUnconfirmedChain(unconfTransactionChainOfCoin);
				mapByTxid.Add(coin.TransactionId, incomeTransaction = new TransactionSummary(coin.Transaction, effectiveFeeRate));
			}
			incomeTransaction.AddOutputCoin(coin.Amount);

			if (coin.SpenderTransaction is { } spenderTransaction)
			{
				var spenderTxId = spenderTransaction.GetHash();

				if (!mapByTxid.TryGetValue(spenderTxId, out TransactionSummary? spendingTransaction))
				{
					// Create if we don't have it yet
					var unconfTransactionChainOfCoin = UnconfirmedTransactionChainProvider.GetUnconfirmedTransactionChain(coin.TransactionId) ?? [];
					var effectiveFeeRate = FeeHelpers.CalculateEffectiveFeeRateOfUnconfirmedChain(unconfTransactionChainOfCoin);
					mapByTxid.Add(spenderTxId, spendingTransaction = new TransactionSummary(spenderTransaction, effectiveFeeRate));
				}
				spendingTransaction.AddInputCoin(coin.Amount);
			}
		}

		return sortForUi
			? mapByTxid.Values.OrderBy(x => x.FirstSeen).ThenBy(x => x.Height).ThenBy(x => x.BlockIndex).ToList()
			: mapByTxid.Values.OrderByBlockchain().ToList();
	}

	/// <summary>
	/// Gets the wallet transaction with the given txid, if the transaction exists.
	/// </summary>
	public bool TryGetTransaction(uint256 txid, [NotNullWhen(true)] out SmartTransaction? smartTransaction)
	{
		// The lock is necessary to make sure that coins registry and transaction store do not change in this code block.
		// The assumption is that the transaction processor is the only component modifying coins registry and transaction store.
		lock (TransactionProcessor.Lock)
		{
			smartTransaction = null;
			bool isKnown = Coins.IsKnown(txid);

			if (isKnown && !BitcoinStore.TransactionStore.TryGetTransaction(txid, out smartTransaction))
			{
				throw new UnreachableException($"{nameof(Coins)} and {nameof(BitcoinStore.TransactionStore)} are not in sync (txid '{txid}').");
			}

			return isKnown;
		}
	}

	public HdPubKey GetNextReceiveAddress(IEnumerable<string> destinationLabels, ScriptPubKeyType type)
	{
		return KeyManager.GetNextReceiveKey(new LabelsArray(destinationLabels), type);
	}

	public int GetPrivacyPercentage()
	{
		var currentPrivacyScore = Coins.Sum(x => x.Amount.Satoshi * Math.Min(x.HdPubKey.AnonymitySet - 1, x.IsPrivate(AnonScoreTarget) ? AnonScoreTarget - 1 : AnonScoreTarget - 2));
		var maxPrivacyScore = Coins.TotalAmount().Satoshi * (AnonScoreTarget - 1);
		int pcPrivate = maxPrivacyScore == 0M ? 0 : (int)(currentPrivacyScore * 100 / maxPrivacyScore);

		return pcPrivate;
	}

	public bool TryLogin(string password, out string? compatibilityPasswordUsed)
	{
		compatibilityPasswordUsed = null;

		if (KeyManager.IsWatchOnly)
		{
			IsLoggedIn = true;
			Kitchen.Cook("");
		}
		else if (PasswordHelper.TryPassword(KeyManager, password, out compatibilityPasswordUsed))
		{
			IsLoggedIn = true;
			Kitchen.Cook(compatibilityPasswordUsed ?? Guard.Correct(password));
		}

		return IsLoggedIn;
	}

	public void Logout()
	{
		Kitchen.CleanUp();
		IsLoggedIn = false;
	}

	public void Initialize()
	{
		if (State > WalletState.WaitingForInit)
		{
			throw new InvalidOperationException($"{nameof(State)} must be {WalletState.Uninitialized} or {WalletState.WaitingForInit}. Current state: {State}.");
		}

		try
		{
			KeyManager.AssertNetworkOrClearBlockState(Network);
			EnsureHeightsAreAtLeastSegWitActivation();

			TransactionProcessor.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessed;
			BitcoinStore.MempoolService.TransactionReceived += Mempool_TransactionReceived;

			State = WalletState.Initialized;
		}
		catch
		{
			State = WalletState.Uninitialized;
			throw;
		}
	}

	/// <inheritdoc/>
	public override async Task StartAsync(CancellationToken cancel)
	{
		if (State != WalletState.Initialized)
		{
			throw new InvalidOperationException($"{nameof(State)} must be {WalletState.Initialized}. Current state: {State}.");
		}

		try
		{
			State = WalletState.Starting;

			await RuntimeParams.LoadAsync().ConfigureAwait(false);

			await WalletFilterProcessor.StartAsync(cancel).ConfigureAwait(false);

			await LoadWalletStateAsync(cancel).ConfigureAwait(false);
			await LoadDummyMempoolAsync().ConfigureAwait(false);
			LoadCoinProperties();

			await base.StartAsync(cancel).ConfigureAwait(false);

			State = WalletState.Started;
		}
		catch
		{
			State = WalletState.Initialized;
			throw;
		}
	}

	private void LoadCoinProperties()
	{
		bool isUpdateRequired = false;
		WalletAttributes attributes = KeyManager.Attributes;
		foreach (var excludedCoin in attributes.ExcludedCoinsFromCoinJoin)
		{
			if (Coins.TryGetByOutPoint(excludedCoin, out SmartCoin? coin))
			{
				coin.IsExcludedFromCoinJoin = true;
			}
			else
			{
				isUpdateRequired = true;
			}
		}
		foreach (var coinjoinCoin in attributes.CoinJoinOutputs)
		{
			if (Coins.TryGetByOutPoint(coinjoinCoin, out SmartCoin? coin))
			{
				coin.IsCoinJoinOutput = true;
			}
			else
			{
				isUpdateRequired = true;
			}
		}
		if (isUpdateRequired)
		{
			KeyManager.UpdateFromCoins(Coins);
		}
	}

	/// <inheritdoc />
	protected override Task ExecuteAsync(CancellationToken stoppingToken)
	{
		Logger.LogInfo($"Wallet '{WalletName}' is fully synchronized.");
		return Task.CompletedTask;
	}

	public string AddCoinJoinPayment(IDestination destination, Money amount)
	{
		var paymentId = BatchedPayments.AddPayment(destination, amount);
		return paymentId.ToString();
	}

	/// <inheritdoc/>
	public override async Task StopAsync(CancellationToken cancel)
	{
		try
		{
			var prevState = State;
			State = WalletState.Stopping;

			if (prevState < WalletState.Stopping)
			{
				await base.StopAsync(cancel).ConfigureAwait(false);

				if (prevState >= WalletState.Initialized)
				{
					await WalletFilterProcessor.StopAsync(cancel).ConfigureAwait(false);
					WalletFilterProcessor.Dispose();

					BitcoinStore.IndexStore.NewFilters -= IndexDownloader_NewFiltersAsync;
					BitcoinStore.MempoolService.TransactionReceived -= Mempool_TransactionReceived;
					TransactionProcessor.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessed;
				}
			}
		}
		finally
		{
			State = WalletState.Stopped;
		}
	}

	private void TransactionProcessor_WalletRelevantTransactionProcessed(object? sender, ProcessedResult e)
	{
		try
		{
			WalletRelevantTransactionProcessed?.Invoke(this, e);
			UnconfirmedTransactionChainProvider.CheckAndScheduleRequestIfNeeded(e.Transaction);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	private void Mempool_TransactionReceived(object? sender, SmartTransaction tx)
	{
		try
		{
			if (!TransactionProcessor.IsAware(tx.GetHash()))
			{
				TransactionProcessor.Process(tx);
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex);
		}
	}

	private async void IndexDownloader_NewFiltersAsync(object? sender, IEnumerable<FilterModel> filters)
	{
		try
		{
			var filterModels = filters as FilterModel[] ?? filters.ToArray();

			await WalletFilterProcessor.ProcessAsync(CancellationToken.None).ConfigureAwait(false);

			NewFiltersProcessed?.Invoke(this, filterModels);
			await Task.Delay(100).ConfigureAwait(false);

			// Make sure fully synced and this filter is the latest filter.
			if (BitcoinStore.SmartHeaderChain.HashesLeft != 0 || BitcoinStore.SmartHeaderChain.TipHash != filterModels.Last().Header.BlockHash)
			{
				return;
			}

			await BitcoinStore.MempoolService.TryPerformMempoolCleanupAsync(Synchronizer.HttpClientFactory).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Cancellation token kicked in while processing the new filters, don't log anything.
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex);
		}
	}

	private async Task LoadWalletStateAsync(CancellationToken cancel)
	{
		// Make sure that the keys are asserted in case of an empty HdPubKeys array.
		KeyManager.GetKeys();

		Height bestTurboSyncHeight = KeyManager.GetBestHeight();

		TransactionProcessor.Process(BitcoinStore.TransactionStore.ConfirmedStore.GetTransactions().TakeWhile(x => x.Height <= bestTurboSyncHeight));

		BitcoinStore.IndexStore.NewFilters += IndexDownloader_NewFiltersAsync;

		// Each time a new batch of filters is downloaded, request a synchronization.
		var lastHashesLeft = BitcoinStore.SmartHeaderChain.HashesLeft;
		while (BitcoinStore.SmartHeaderChain.HashesLeft > 0)
		{
			cancel.ThrowIfCancellationRequested();
			if (lastHashesLeft == BitcoinStore.SmartHeaderChain.HashesLeft)
			{
				await Task.Delay(100, cancel).ConfigureAwait(false);
				continue;
			}
			lastHashesLeft = BitcoinStore.SmartHeaderChain.HashesLeft;
			await PerformSynchronizationAsync(cancel).ConfigureAwait(false);
		}

		// Request a synchronization once all filters were downloaded.
		await PerformSynchronizationAsync(cancel).ConfigureAwait(false);
	}

	public async Task PerformSynchronizationAsync(CancellationToken cancellationToken)
	{
		await WalletFilterProcessor.ProcessAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task LoadDummyMempoolAsync()
	{
		if (BitcoinStore.TransactionStore.MempoolStore.IsEmpty())
		{
			return;
		}

		// Only clean the mempool if we're fully synchronized.
		if (BitcoinStore.SmartHeaderChain.HashesLeft == 0)
		{
			try
			{
				var client = Synchronizer.HttpClientFactory.SharedWasabiClient;
				var compactness = 10;

				var mempoolHashes = await client.GetMempoolHashesAsync(compactness).ConfigureAwait(false);

				var txsToProcess = new List<SmartTransaction>();
				foreach (var tx in BitcoinStore.TransactionStore.MempoolStore.GetTransactions())
				{
					uint256 txid = tx.GetHash();
					if (mempoolHashes.Contains(txid.ToString()[..compactness]))
					{
						txsToProcess.Add(tx);
						Logger.LogInfo($"'{WalletName}': Transaction was successfully tested against the backend's mempool hashes: {txid}.");
					}
					else
					{
						BitcoinStore.TransactionStore.MempoolStore.TryRemove(txid, out _);
					}
				}

				TransactionProcessor.Process(txsToProcess);
			}
			catch (Exception ex)
			{
				// When there's a connection failure do not clean the transactions, add them to processing.
				TransactionProcessor.Process(BitcoinStore.TransactionStore.MempoolStore.GetTransactions());

				Logger.LogWarning(ex);
			}
		}
		else
		{
			TransactionProcessor.Process(BitcoinStore.TransactionStore.MempoolStore.GetTransactions());
		}
	}

	public void SetWaitingForInitState()
	{
		if (State != WalletState.Uninitialized)
		{
			throw new InvalidOperationException($"{nameof(State)} must be {WalletState.Uninitialized}. Current state: {State}.");
		}

		State = WalletState.WaitingForInit;
	}

	public void ExcludeCoinFromCoinJoin(OutPoint outpoint, bool exclude = true)
	{
		if (!Coins.TryGetByOutPoint(outpoint, out var coin))
		{
			throw new InvalidOperationException($"Coin '{outpoint}' doesn't belong to the wallet or is spent.");
		}

		coin.IsExcludedFromCoinJoin = exclude;
		KeyManager.UpdateFromCoins(Coins);
	}

	public void UpdateExcludedCoinsFromCoinJoin(OutPoint[] outPointsToExclude)
	{
		foreach (var coin in Coins)
		{
			coin.IsExcludedFromCoinJoin = outPointsToExclude.Contains(coin.Outpoint);
		}

		KeyManager.UpdateFromCoins(Coins);
	}

	public void UpdateUsedHdPubKeysLabels(Dictionary<HdPubKey, LabelsArray> hdPubKeysWithLabels)
	{
		if (hdPubKeysWithLabels.Count == 0)
		{
			return;
		}

		foreach (var item in hdPubKeysWithLabels)
		{
			item.Key.SetLabel(item.Value);
		}

		KeyManager.ToFile();
	}

	public void AddCoinJoinTransaction(uint256 txHash)
	{
		if (TransactionProcessor.IsAware(txHash))
		{
			Coins.Where(x => x.TransactionId == txHash).ForEach(x => x.IsCoinJoinOutput = true);
			KeyManager.UpdateFromCoins(Coins);
		}
		else
		{
			KeyManager.AddCoinJoinTransaction(txHash);
		}
	}

	private void EnsureHeightsAreAtLeastSegWitActivation()
	{
		var startingSegwitHeight = new Height(SmartHeader.GetStartingHeader(Network, IndexType.SegwitTaproot).Height);
		if (startingSegwitHeight > KeyManager.GetBestHeight())
		{
			KeyManager.SetBestHeight(startingSegwitHeight);
		}
	}
}
