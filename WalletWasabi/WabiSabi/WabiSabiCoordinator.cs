using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.DoSPrevention;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.WabiSabi.Recommendation;

namespace WalletWasabi.WabiSabi;

public class WabiSabiCoordinator : BackgroundService
{
	public WabiSabiCoordinator(CoordinatorParameters parameters, IRPCClient rpc, ICoinJoinIdStore coinJoinIdStore, CoinJoinScriptStore coinJoinScriptStore, IHttpClientFactory httpClientFactory, DenominationFactory? denominationFactory = null, CoinVerifier? coinVerifier = null, MiningFeeRateEstimator? miningFeeRateEstimator = null)
	{
		Parameters = parameters;
		RpcClient = rpc;
		Warden = new(parameters.PrisonFilePath, coinJoinIdStore, Config);
		ConfigWatcher = new(parameters.ConfigChangeMonitoringPeriod, Config, () => Logger.LogInfo("WabiSabi configuration has changed."));
		CoinJoinIdStore = coinJoinIdStore;
		CoinJoinScriptStore = coinJoinScriptStore;
		CoinVerifier = coinVerifier;
		CoinJoinTransactionArchiver transactionArchiver = new(Path.Combine(parameters.CoordinatorDataDir, "CoinJoinTransactions"));

		CoinJoinFeeRateStatStore = CoinJoinFeeRateStatStore.LoadFromFile(parameters.CoinJoinFeeRateStatStoreFilePath, Config, rpc);
		IoHelpers.EnsureContainingDirectoryExists(Parameters.CoinJoinFeeRateStatStoreFilePath);
		CoinJoinFeeRateStatStore.NewStat += FeeRateStatStore_NewStat;

		IoHelpers.EnsureContainingDirectoryExists(Parameters.CoinJoinScriptStoreFilePath);

		RoundParameterFactory roundParameterFactory = new(Config, rpc.Network);
		Arena = WabiSabiBackendFactory.Instance.CreateArena(
			parameters.RoundProgressSteppingPeriod,
			Config,
			rpc,
			Warden.Prison,
			coinJoinIdStore,
			roundParameterFactory,
			denominationFactory,
			transactionArchiver,
			coinJoinScriptStore,
			coinVerifier,
			miningFeeRateEstimator);

		IoHelpers.EnsureContainingDirectoryExists(Parameters.CoinJoinIdStoreFilePath);
		Arena.CoinJoinBroadcast += Arena_CoinJoinBroadcast;
	}

	public ConfigWatcher ConfigWatcher { get; }
	public ICoinJoinIdStore CoinJoinIdStore { get; private set; }
	public CoinJoinScriptStore CoinJoinScriptStore { get; private set; }
	public CoinVerifier? CoinVerifier { get; private set; }
	public Warden Warden { get; }

	public CoordinatorParameters Parameters { get; }
	public Arena Arena { get; }

	public CoinJoinFeeRateStatStore CoinJoinFeeRateStatStore { get; }

	public WabiSabiConfig Config => Parameters.RuntimeCoordinatorConfig;
	public DateTimeOffset LastSuccessfulCoinJoinTime { get; private set; } = DateTimeOffset.UtcNow;
	private IRPCClient RpcClient { get; }

	private void Arena_CoinJoinBroadcast(object? sender, Transaction transaction)
	{
		LastSuccessfulCoinJoinTime = DateTimeOffset.UtcNow;

		CoinJoinIdStore.TryAdd(transaction.GetHash());
		CoinJoinScriptStore.AddRange(transaction.Outputs.Select(x => x.ScriptPubKey));

		var coinJoinScriptStoreFilePath = Parameters.CoinJoinScriptStoreFilePath;
		try
		{
			File.AppendAllLines(coinJoinScriptStoreFilePath, transaction.Outputs.Select(x => x.ScriptPubKey.ToHex()));
		}
		catch (Exception ex)
		{
			Logger.LogError($"Could not write file {coinJoinScriptStoreFilePath}.", ex);
		}
	}

	private void FeeRateStatStore_NewStat(object? sender, CoinJoinFeeRateStat feeRateStat)
	{
		var filePath = Parameters.CoinJoinFeeRateStatStoreFilePath;
		try
		{
			File.AppendAllLines(filePath, new[] { feeRateStat.ToLine() });
		}
		catch (Exception ex)
		{
			Logger.LogError($"Could not write file {filePath}.", ex);
		}
	}

	protected virtual bool DescendantCoinJoinCheck(Transaction tx)
	{
		return CoinJoinIdStore.Contains(tx.GetHash());
	}

	protected virtual bool DoubleSpenderCoinJoinCheck(Transaction tx)
	{
		var txId = tx.GetHash();
		return CoinJoinIdStore.Contains(txId) || IsFinishedCoinJoin(txId) || IsWasabiCoinJoinLookingTx(tx);
	}

	public void BanDescendant(object? sender, Block block)
	{
		var now = DateTimeOffset.UtcNow;

		bool IsInputBanned(TxIn input) => Warden.Prison.IsBanned(input.PrevOut, Config.GetDoSConfiguration(), now);
		OutPoint[] BannedInputs(Transaction tx) => tx.Inputs.Where(IsInputBanned).Select(x => x.PrevOut).ToArray();

		var outpointsToBan = block.Transactions
			.Where(tx => !DescendantCoinJoinCheck(tx))  // We don't ban coinjoin outputs
			.Select(tx => (Tx: tx, BannedInputs: BannedInputs(tx)))
			.Where(x => x.BannedInputs.Length != 0)
			.SelectMany(x => x.Tx.Outputs.Select((_, i) => (new OutPoint(x.Tx, i), x.BannedInputs)));

		foreach (var (outpoint, ancestors) in outpointsToBan)
		{
			Warden.Prison.InheritPunishment(outpoint, ancestors);
		}
	}

	public async void BanDoubleSpenders(object? sender, Transaction tx)
	{
		try
		{
			if (DoubleSpenderCoinJoinCheck(tx))
			{
				return;
			}

			var inputOutPoints = tx.Inputs.Select(x => x.PrevOut);
			var disruptedRounds = Arena.GetRoundsContainingOutpoints(inputOutPoints);

			// No round was disrupted by the received transaction. Nothing to do here.
			if (disruptedRounds.Length == 0)
			{
				return;
			}

			// Ban all outputs created by the received transaction because it has spent coins participating in coinjoin rounds.
			foreach (var indexedOutput in tx.Outputs.AsIndexedOutputs())
			{
				Warden.Prison.DoubleSpent(
					new OutPoint(tx, indexedOutput.N),
					indexedOutput.TxOut.Value,
					disruptedRounds.Select(x => x.RoundId));
			}

			// Abort disrupted rounds (only those that pay less than the attacking transaction)
			using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
			var (succeed, spentCoins) = await GetSpendingCoinsAsync(inputOutPoints, cts.Token).ConfigureAwait(false);
			var feeRate = succeed ? tx.GetFeeRate(spentCoins) : Constants.AbsurdlyHighFeeRate;
			var roundsToAbort = disruptedRounds
				.Where(round => round.MiningFeeRate < feeRate)
				.Select(x => x.RoundId);

			foreach (var roundId in roundsToAbort)
			{
				Arena.AbortRound(roundId);
			}
		}
		catch (Exception e)
		{
			Logger.LogError(e);
		}
	}

	private async Task<(bool, Coin[])> GetSpendingCoinsAsync(
		IEnumerable<OutPoint> spendingOutPoints,
		CancellationToken cancellationToken)
	{
		try
		{
			var batch = RpcClient.PrepareBatch();
			var getTxOutRequests = spendingOutPoints
				.Select(x => RpcClient.GetTxOutAsync(x.Hash, (int)x.N, includeMempool: true, cancellationToken))
				.ToList();
			await batch.SendBatchAsync(cancellationToken).ConfigureAwait(false);
			var txOutResponses = await Task.WhenAll(getTxOutRequests).ConfigureAwait(false);

			// If not all txout are found then we cannot calculate the fee rate
			if (txOutResponses.Any(x => x is null))
			{
				return (false, Array.Empty<Coin>());
			}

			var txOuts = txOutResponses.Select(x => x.TxOut);
			var spendingCoins = txOuts
				.Zip(spendingOutPoints, (txOut, outPoint) => (txOut, outPoint))
				.Select(x => new Coin(x.outPoint, x.txOut))
				.ToArray();
			return (true, spendingCoins);
		}
		catch (Exception e)
		{
			Logger.LogError(e);
			return (false, Array.Empty<Coin>());
		}
	}

	protected bool IsFinishedCoinJoin(uint256 txId) =>
		Arena.RoundStates
		.Select(x => x.CoinjoinState)
		.OfType<SigningState>()
		.Any(x => x.CreateUnsignedTransaction().GetHash() == txId);

	protected bool IsWasabiCoinJoinLookingTx(Transaction tx)
	{
		if (tx.RBF
		|| tx.Inputs.Count < 21
		|| tx.Inputs.Count > 500
		|| tx.Outputs.Count < 15
		|| tx.Outputs.Count > 500)
		{
			return false;
		}

		HashSet<Money> amounts = new();
		foreach (var output in tx.Outputs)
		{
			amounts.Add(output.Value);
		}

		var anonScore = tx.Outputs.Count / amounts.Count;
		if (anonScore < 2)
		{
			return false;
		}

		return true;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await ConfigWatcher.StartAsync(stoppingToken).ConfigureAwait(false);
		await Warden.StartAsync(stoppingToken).ConfigureAwait(false);
		await Arena.StartAsync(stoppingToken).ConfigureAwait(false);

		await CoinJoinFeeRateStatStore.StartAsync(stoppingToken).ConfigureAwait(false);
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await base.StopAsync(cancellationToken).ConfigureAwait(false);

		await Arena.StopAsync(cancellationToken).ConfigureAwait(false);
		await ConfigWatcher.StopAsync(cancellationToken).ConfigureAwait(false);
		await Warden.StopAsync(cancellationToken).ConfigureAwait(false);

		await CoinJoinFeeRateStatStore.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	public override void Dispose()
	{
		CoinJoinFeeRateStatStore.NewStat -= FeeRateStatStore_NewStat;
		Arena.CoinJoinBroadcast -= Arena_CoinJoinBroadcast;
		ConfigWatcher.Dispose();
		Warden.Dispose();
		base.Dispose();
	}
}
