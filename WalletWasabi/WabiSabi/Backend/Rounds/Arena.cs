using GingerCommon.Logging;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.DoSPrevention;
using WalletWasabi.WabiSabi.Backend.Events;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.WabiSabi.Recommendation;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public partial class Arena : PeriodicRunner
{
	public Arena(
		TimeSpan period,
		WabiSabiConfig config,
		IRPCClient rpc,
		Prison prison,
		ICoinJoinIdStore coinJoinIdStore,
		RoundParameterFactory roundParameterFactory,
		DenominationFactory? denominationFactory = null,
		CoinJoinTransactionArchiver? archiver = null,
		CoinJoinScriptStore? coinJoinScriptStore = null,
		CoinVerifier? coinVerifier = null,
		MiningFeeRateEstimator? miningFeeRateEstimator = null) : base(period)
	{
		Config = config;
		Rpc = rpc;
		Prison = prison;
		TransactionArchiver = archiver;
		CoinJoinIdStore = coinJoinIdStore;
		CoinJoinScriptStore = coinJoinScriptStore;
		MiningFeeRateEstimator = miningFeeRateEstimator ?? new MiningFeeRateEstimator(config, rpc);
		RoundParameterFactory = roundParameterFactory;
		CoinVerifier = coinVerifier;
		MaxSuggestedAmountProvider = new(Config);

		if (CoinVerifier is not null)
		{
			CoinVerifier.CoinBlacklisted += CoinVerifier_CoinBlacklisted;
		}

		DenominationFactory = denominationFactory;
	}

	public event EventHandler<Transaction>? CoinJoinBroadcast;

	public event EventHandler<RoundCreatedEventArgs>? RoundCreated;

	public event EventHandler<CoinJoinTransactionCreatedEventArgs>? CoinJoinTransactionCreated;

	public event EventHandler<RoundPhaseChangedEventArgs>? RoundPhaseChanged;

	public event EventHandler<AffiliationAddedEventArgs>? AffiliationAdded;

	public event EventHandler<InputAddedEventArgs>? InputAdded;

	public HashSet<Round> Rounds { get; } = new();
	public ImmutableList<RoundState> RoundStates { get; private set; } = ImmutableList<RoundState>.Empty;
	internal ConcurrentQueue<uint256> DisruptedRounds { get; } = new();
	private AsyncLock AsyncLock { get; } = new();
	protected WabiSabiConfig Config { get; }
	internal IRPCClient Rpc { get; }
	private Prison Prison { get; }
	private CoinJoinTransactionArchiver? TransactionArchiver { get; }
	public CoinJoinScriptStore? CoinJoinScriptStore { get; }
	public CoinVerifier? CoinVerifier { get; private set; }
	private ICoinJoinIdStore CoinJoinIdStore { get; set; }
	protected MiningFeeRateEstimator MiningFeeRateEstimator { get; set; }
	protected RoundParameterFactory RoundParameterFactory { get; }
	public MaxSuggestedAmountProvider MaxSuggestedAmountProvider { get; }

	public DenominationFactory? DenominationFactory { get; }

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		using (await AsyncLock.LockAsync(cancel).ConfigureAwait(false))
		{
			var beforeInside = DateTimeOffset.UtcNow;
			TimeoutRounds();

			TimeoutAlices();

			await StepTransactionSigningPhaseAsync(cancel).ConfigureAwait(false);

			StepOutputRegistrationPhase();

			await StepConnectionConfirmationPhaseAsync(cancel).ConfigureAwait(false);

			await StepInputRegistrationPhaseAsync(cancel).ConfigureAwait(false);

			cancel.ThrowIfCancellationRequested();

			// Ensure there's at least one non-blame round in input registration.
			await CreateRoundsAsync(cancel).ConfigureAwait(false);

			AbortDisruptedRounds();

			// RoundStates have to contain all states. Do not change stateId=0.
			SetRoundStates();

			await MiningFeeRateEstimator.LogMiningFeeRatesAsync(false, cancel).ConfigureAwait(false);
		}
	}

	private void SetRoundStates()
	{
		// Order rounds ascending by max suggested amount, then ascending by input count.
		// This will make sure WW2.0.1 clients register according to our desired order.
		var rounds = Rounds
						.OrderBy(x => x.Parameters.MaxSuggestedAmount)
						.ThenBy(x => x.InputCount)
						.ToList();

		RoundStates = rounds.Select(r => RoundState.FromRound(r, stateId: 0)).ToImmutableList();
	}

	private async Task StepInputRegistrationPhaseAsync(CancellationToken cancel)
	{
		foreach (var round in Rounds.Where(x =>
			x.Phase == Phase.InputRegistration
			&& x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound))
			.ToArray())
		{
			try
			{
				await foreach (var offendingAlices in CheckTxoSpendStatusAsync(round, cancel).ConfigureAwait(false))
				{
					if (offendingAlices.Length != 0)
					{
						round.Alices.RemoveAll(x => offendingAlices.Contains(x));
					}
				}

				if (round is not BlameRound && CoinVerifier is not null)
				{
					try
					{
						var coinAliceDictionary = round.Alices.ToDictionary(alice => alice.Coin, alice => alice, CoinEqualityComparer.Default);
						foreach (var coinVerifyInfo in await CoinVerifier.VerifyCoinsAsync(coinAliceDictionary.Keys, cancel).ConfigureAwait(false))
						{
							if (coinVerifyInfo.ShouldRemove)
							{
								round.Alices.Remove(coinAliceDictionary[coinVerifyInfo.Coin]);
								Logger.LogInfo($"{round.Id} {coinVerifyInfo.Coin.Outpoint} got removed from round");
							}
						}
					}
					catch (Exception exc)
					{
						// This should never happen.
						CoinVerifier.VerifierAuditArchiver.LogException(round.Id, "AliceException", exc);
						Logger.LogDiscord(LogLevel.Error, $"AliceException at round {round.Id}: {exc.Message}", normalLogLevel: LogLevel.Error);
						throw;
					}
				}

				if (round.InputRegistrationTimeFrame.HasExpired || round.IsInputRegistrationEnded(round.Parameters.MaxInputCountByRound))
				{
					await EndOfInputRegistrationAsync(round, cancel).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				EndRound(round, EndRoundState.AbortedWithError);
				round.LogError(ex.Message);
			}
		}
	}

	// Called at the end of the input registration
	protected virtual Task EndOfInputRegistrationAsync(Round round, CancellationToken cancel)
	{
		if (round.InputCount < round.Parameters.MinInputCountByRound)
		{
			MaxSuggestedAmountProvider.StepMaxSuggested(round, false);
			EndRound(round, EndRoundState.AbortedNotEnoughAlices);
			round.LogInfo($"Not enough inputs ({round.InputCount}) in {nameof(Phase.InputRegistration)} phase. The minimum is ({round.Parameters.MinInputCountByRound}). {nameof(round.Parameters.MaxSuggestedAmount)} was '{round.Parameters.MaxSuggestedAmount}' BTC.");
		}
		else if (round.IsInputRegistrationEnded(round.Parameters.MaxInputCountByRound))
		{
			MaxSuggestedAmountProvider.StepMaxSuggested(round, true);
			SetRoundPhase(round, Phase.ConnectionConfirmation);
		}

		return Task.CompletedTask;
	}

	protected virtual void CreateRecommendations(Round round)
	{
		// We don't leak information about whether an input is exempt from fee or not
		DateTime time = DateTime.UtcNow;
		var inputs = round.Alices.Select(x => x.Coin.EffectiveValue(round.Parameters.MiningFeeRate, round.Parameters.CoordinationFeeRate)).ToList();
		round.LogInfo($"Inputs for the recommended denominations: [{inputs.ListToString()}]");
		var defaultDenoms = DenominationFactory?.CreateDefaultDenominations(inputs, round.Parameters.MiningFeeRate) ?? [];
		round.LogInfo($"Default denomination levels ({defaultDenoms.Count,2}):     [{defaultDenoms.ListToString()}]");
		var denoms = DenominationFactory?.CreatePreferedDenominations(inputs, round.Parameters.MiningFeeRate) ?? [];
		round.LogInfo($"Recommended denomination levels ({denoms.Count,2}): [{denoms.ListToString()}]");
		var freqs = DenominationFactory?.CreateDenominationFrequencies(inputs, round.Parameters.MiningFeeRate, denoms) ?? [];
		round.LogInfo($"Estimated frequencies for the denomination levels: [{freqs.ListToString("F2")}]");
		round.Denomination = denoms.ToImmutableSortedSet();
		round.DenominationFrequencies = freqs.ToImmutableList();
		round.LogInfo($"Recommandations were created in {(DateTime.UtcNow - time).TotalSeconds:F3} secs.");
	}

	private async Task StepConnectionConfirmationPhaseAsync(CancellationToken cancel)
	{
		foreach (var round in Rounds.Where(x => x.Phase == Phase.ConnectionConfirmation).ToArray())
		{
			try
			{
				if (round.Alices.All(x => x.ConfirmedConnection))
				{
					CreateRecommendations(round);
					SetRoundPhase(round, Phase.OutputRegistration);
				}
				else if (round.ConnectionConfirmationTimeFrame.HasExpired)
				{
					var alicesDidNotConfirm = round.Alices.Where(x => !x.ConfirmedConnection).ToArray();
					if (ReasonableOffendersCount(alicesDidNotConfirm.Length, round.Parameters.MinInputCountByRound))
					{
						foreach (var alice in alicesDidNotConfirm)
						{
							Prison.FailedToConfirm(alice.Coin.Outpoint, alice.Coin.Amount, round.Id);
						}
					}
					else
					{
						Logger.LogWarning($"{round.Id}: Tried to ban {alicesDidNotConfirm.Length} inputs for FailedToConfirm - ban was skipped.");
						foreach (var alice in alicesDidNotConfirm)
						{
							Prison.BackendStabilitySafetyBan(alice.Coin.Outpoint, round.Id);
						}
					}
					var removedAliceCount = round.Alices.RemoveAll(x => alicesDidNotConfirm.Contains(x));
					round.LogInfo($"{removedAliceCount} alices removed because they didn't confirm.");

					// Once an input is confirmed and non-zero credentials are issued, it is too late to do any
					if (round.InputCount >= round.Parameters.MinInputCountByRound)
					{
						var allOffendingAlices = new List<Alice>();
						await foreach (var offendingAlices in CheckTxoSpendStatusAsync(round, cancel).ConfigureAwait(false))
						{
							allOffendingAlices.AddRange(offendingAlices);
						}

						if (ReasonableOffendersCount(allOffendingAlices.Count, round.Parameters.MinInputCountByRound))
						{
							foreach (var offender in allOffendingAlices)
							{
								Prison.DoubleSpent(offender.Coin.Outpoint, offender.Coin.Amount, round.Id);
							}
						}
						else
						{
							Logger.LogWarning($"{round.Id}: Tried to ban {allOffendingAlices.Count} inputs for FailedToConfirm - ban was skipped.");
							foreach (var alice in allOffendingAlices)
							{
								Prison.BackendStabilitySafetyBan(alice.Coin.Outpoint, round.Id);
							}
						}
						if (allOffendingAlices.Count > 0)
						{
							round.LogInfo($"There were {allOffendingAlices.Count} alices that spent the registered UTXO. Aborting...");

							await EndRoundAndTryCreateBlameRoundAsync(round, cancel).ConfigureAwait(false);
							return;
						}
					}

					if (round.InputCount < round.Parameters.MinInputCountByRound)
					{
						EndRound(round, EndRoundState.AbortedNotEnoughAlices);
						round.LogInfo($"Not enough inputs ({round.InputCount}) in {nameof(Phase.ConnectionConfirmation)} phase. The minimum is ({round.Parameters.MinInputCountByRound}).");
					}
					else
					{
						CreateRecommendations(round);
						round.OutputRegistrationTimeFrame = TimeFrame.Create(Config.FailFastOutputRegistrationTimeout);
						SetRoundPhase(round, Phase.OutputRegistration);
					}
				}
			}
			catch (Exception ex)
			{
				EndRound(round, EndRoundState.AbortedWithError);
				round.LogError(ex.Message);
			}
		}
	}

	private void StepOutputRegistrationPhase()
	{
		foreach (var round in Rounds.Where(x => x.Phase == Phase.OutputRegistration).ToArray())
		{
			try
			{
				var allReady = round.Alices.All(a => a.ReadyToSign);
				bool phaseExpired = round.OutputRegistrationTimeFrame.HasExpired;

				if (allReady || phaseExpired)
				{
					var coinjoin = round.Assert<ConstructionState>();

					round.LogInfo($"{coinjoin.Inputs.Count()} inputs were added.");
					round.LogInfo($"{coinjoin.Outputs.Count()} outputs were added.");

					round.CoordinatorScript = GetCoordinatorScriptPreventReuse(round);
					coinjoin = AddCoordinationFee(round, coinjoin, round.CoordinatorScript);

					round.CoinjoinState = FinalizeTransaction(round.Id, coinjoin);

					if (!allReady && phaseExpired)
					{
						// It would be better to end the round and create a blame round here, but older client would not support it.
						// See https://github.com/zkSNACKs/WalletWasabi/pull/11028.
						round.TransactionSigningTimeFrame = TimeFrame.Create(Config.FailFastTransactionSigningTimeout);
						round.FastSigningPhase = true;
					}

					SetRoundPhase(round, Phase.TransactionSigning);
				}
			}
			catch (Exception ex)
			{
				EndRound(round, EndRoundState.AbortedWithError);
				round.LogError(ex.Message);
			}
		}
	}

	private async Task StepTransactionSigningPhaseAsync(CancellationToken cancellationToken)
	{
		foreach (var round in Rounds.Where(x => x.Phase == Phase.TransactionSigning).ToArray())
		{
			var state = round.Assert<SigningState>();

			try
			{
				if (state.IsFullySigned)
				{
					Transaction coinjoin = state.CreateTransaction();

					// Logging.
					round.LogInfo("Trying to broadcast coinjoin.");
					Coin[] spentCoins = round.CoinjoinState.Inputs.ToArray();
					Money networkFee = coinjoin.GetFee(spentCoins);
					round.LogInfo($"Network Fee: {networkFee.ToString(false, false)} BTC.");
					uint256 roundId = round.Id;
					FeeRate feeRate = coinjoin.GetFeeRate(spentCoins);
					round.LogInfo($"Network Fee Rate: {feeRate.SatoshiPerByte} sat/vByte.");
					round.LogInfo($"Desired Fee Rate: {round.Parameters.MiningFeeRate.SatoshiPerByte} sat/vByte.");

					// Added for monitoring reasons.
					try
					{
						FeeRate targetFeeRate = (await Rpc.EstimateConservativeSmartFeeAsync((int)Config.ConfirmationTarget, cancellationToken).ConfigureAwait(false)).FeeRate;
						round.LogInfo($"Current Fee Rate on the Network: {targetFeeRate.SatoshiPerByte} sat/vByte. Confirmation target is: {(int)Config.ConfirmationTarget} blocks.");
					}
					catch (Exception ex)
					{
						Logger.LogDebug($"Could not log fee rate monitoring: '{ex.Message}'.");
					}

					round.LogInfo($"Number of inputs: {coinjoin.Inputs.Count}.");
					round.LogInfo($"Number of outputs: {coinjoin.Outputs.Count}.");
					int size = coinjoin.GetSerializedSize();
					round.LogInfo($"Serialized Size: {size / 1024.0,7:F3} KB. ({size,6}B)");
					int vsize = coinjoin.GetVirtualSize();
					round.LogInfo($"Virtual Size:    {vsize / 1024.0,7:F3} KB. ({vsize,6}vB)");
					var indistinguishableOutputs = coinjoin.GetIndistinguishableOutputs(includeSingle: true);
					foreach (var (value, count) in indistinguishableOutputs.Where(x => x.count > 1))
					{
						round.LogInfo($"There are {count} occurrences of {value.ToString(true, false)} outputs.");
					}

					round.LogInfo(
						$"There are {indistinguishableOutputs.Count(x => x.count == 1)} occurrences of unique outputs.");

					// Store transaction.
					if (TransactionArchiver is not null)
					{
						await TransactionArchiver.StoreJsonAsync(coinjoin).ConfigureAwait(false);
					}

					// Broadcasting.
					await Rpc.SendRawTransactionAsync(coinjoin, cancellationToken).ConfigureAwait(false);
					EndRound(round, EndRoundState.TransactionBroadcasted);
					round.LogInfo($"Successfully broadcast the coinjoin: {coinjoin.GetHash()}.");
					round.LogInfo($"Coinjoin summary {coinjoin.GetHash()}, {coinjoin.Inputs.Count}, {coinjoin.Outputs.Count}, {coinjoin.GetAnonScore():F2}, {state.Inputs.Select(x => x.Amount).Sum().Satoshi}, {coinjoin.Outputs.Select(x => x.Value).Sum().Satoshi}, {round.ExpectedCoordinationFee.Satoshi}, {round.CoordinationFee.Satoshi}, {feeRate.SatoshiPerByte}");

					var coordinatorScriptPubKey = Config.GetNextCleanCoordinatorScript();
					if (round.CoordinatorScript == coordinatorScriptPubKey)
					{
						Config.MakeNextCoordinatorScriptDirty();
					}

					foreach (var address in coinjoin.Outputs
						.Select(x => x.ScriptPubKey)
						.Where(script => CoinJoinScriptStore?.Contains(script) is true))
					{
						if (address == round.CoordinatorScript)
						{
							round.LogError(
								$"Coordinator script pub key reuse detected: {round.CoordinatorScript.ToHex()}");
						}
						else
						{
							round.LogError($"Output script pub key reuse detected: {address.ToHex()}");
						}
					}
					CoinJoinBroadcast?.Invoke(this, coinjoin);
				}
				else if (round.TransactionSigningTimeFrame.HasExpired)
				{
					round.LogWarning($"Signing phase failed with timed out after {round.TransactionSigningTimeFrame.Duration.TotalSeconds} seconds.");
					if (round.FastSigningPhase)
					{
						await FailFastTransactionSigningPhaseAsync(round, cancellationToken).ConfigureAwait(false);
					}
					else
					{
						await FailTransactionSigningPhaseAsync(round, cancellationToken).ConfigureAwait(false);
					}
				}
			}
			catch (RPCException ex)
			{
				round.LogError($"Transaction broadcasting failed: '{ex}'.");
				EndRound(round, EndRoundState.TransactionBroadcastFailed);
			}
			catch (Exception ex)
			{
				round.LogWarning($"Signing phase failed, reason: '{ex}'.");
				EndRound(round, EndRoundState.AbortedWithError);
			}
		}
	}

	private async IAsyncEnumerable<Alice[]> CheckTxoSpendStatusAsync(Round round, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var chunkOfAlices in round.Alices.ToList().ChunkBy(16))
		{
			var batchedRpc = Rpc.PrepareBatch();

			var aliceCheckingTaskPairs = chunkOfAlices
				.Select(x => (Alice: x, StatusTask: Rpc.GetTxOutAsync(x.Coin.Outpoint.Hash, (int)x.Coin.Outpoint.N, includeMempool: true, cancellationToken)))
				.ToList();

			await batchedRpc.SendBatchAsync(cancellationToken).ConfigureAwait(false);

			var spendStatusCheckingTasks = aliceCheckingTaskPairs.Select(async x => (x.Alice, Status: await x.StatusTask.ConfigureAwait(false)));
			var alices = await Task.WhenAll(spendStatusCheckingTasks).ConfigureAwait(false);
			yield return alices.Where(x => x.Status is null).Select(x => x.Alice).ToArray();
		}
	}

	private async Task FailTransactionSigningPhaseAsync(Round round, CancellationToken cancellationToken)
	{
		var state = round.Assert<SigningState>();

		var unsignedOutpoints = state.UnsignedInputs.Select(c => c.Outpoint).ToHashSet();

		var alicesWhoDidNotSign = round.Alices
			.Where(alice => unsignedOutpoints.Contains(alice.Coin.Outpoint))
			.ToHashSet();

		if (ReasonableOffendersCount(alicesWhoDidNotSign.Count, round.Parameters.MinInputCountByRound))
		{
			foreach (var alice in alicesWhoDidNotSign)
			{
				Prison.FailedToSign(alice.Coin.Outpoint, alice.Coin.Amount, round.Id);
			}
		}
		else
		{
			Logger.LogWarning($"{round.Id}: Tried to ban {alicesWhoDidNotSign.Count} inputs for FailedToConfirm - ban was skipped.");
			foreach (var alice in alicesWhoDidNotSign)
			{
				Prison.BackendStabilitySafetyBan(alice.Coin.Outpoint, round.Id);
			}
		}

		var cnt = round.Alices.RemoveAll(alice => unsignedOutpoints.Contains(alice.Coin.Outpoint));

		round.LogInfo($"Removed {cnt} alices, because they didn't sign. Remaining: {round.InputCount}");

		await EndRoundAndTryCreateBlameRoundAsync(round, cancellationToken).ConfigureAwait(false);
	}

	private async Task FailFastTransactionSigningPhaseAsync(Round round, CancellationToken cancellationToken)
	{
		var alicesToRemove = round.Alices.Where(alice => !alice.ReadyToSign).ToHashSet();

		if (ReasonableOffendersCount(alicesToRemove.Count, round.Parameters.MinInputCountByRound))
		{
			foreach (var alice in alicesToRemove)
			{
				// Intentionally, do not ban Alices who have not signed, as clients using hardware wallets may not be able to sign in time.
				Prison.FailedToSignalReadyToSign(alice.Coin.Outpoint, alice.Coin.Amount, round.Id);
			}
		}
		else
		{
			Logger.LogWarning($"{round.Id}: Tried to ban {alicesToRemove.Count} inputs for FailedToConfirm - ban was skipped.");
			foreach (var alice in alicesToRemove)
			{
				Prison.BackendStabilitySafetyBan(alice.Coin.Outpoint, round.Id);
			}
		}

		var removedAlices = round.Alices.RemoveAll(alice => alicesToRemove.Contains(alice));

		round.LogInfo($"Removed {removedAlices} alices, because they weren't ready. Remaining: {round.InputCount}");

		await EndRoundAndTryCreateBlameRoundAsync(round, cancellationToken).ConfigureAwait(false);
	}

	private async Task EndRoundAndTryCreateBlameRoundAsync(Round round, CancellationToken cancellationToken)
	{
		if (round.InputCount < Config.MinInputCountByBlameRound)
		{
			// There are not enough inputs, makes no sense to create the blame round.
			EndRound(round, EndRoundState.AbortedNotEnoughAlicesSigned);
			return;
		}

		// This indicates to the client that there will be a blame round.
		EndRound(round, EndRoundState.NotAllAlicesSign);

		var feeRate = await MiningFeeRateEstimator.GetRoundFeeRateAsync(cancellationToken).ConfigureAwait(false);
		var blameWhitelist = round.Alices
			.Select(x => x.Coin.Outpoint)
			.Where(x => !Prison.IsBanned(x, Config.GetDoSConfiguration(), DateTimeOffset.UtcNow))
			.ToHashSet();

		RoundParameters parameters = RoundParameterFactory.CreateBlameRoundParameter(feeRate, round) with
		{
			MinInputCountByRound = Config.MinInputCountByBlameRound
		};

		BlameRound blameRound = new(parameters, round, blameWhitelist, SecureRandom.Instance);
		AddRound(blameRound);
		blameRound.LogInfo($"Blame round created from round '{round.Id}'.");
	}

	private async Task CreateRoundsAsync(CancellationToken cancellationToken)
	{
		FeeRate? feeRate = null;

		// Have rounds to split the volume around minimum input counts if load balance is required.
		// Only do things if the load balancer compatibility is configured.
		if (Config.WW200CompatibleLoadBalancing)
		{
			foreach (var round in Rounds.Where(x =>
				x.Phase == Phase.InputRegistration
				&& x is not BlameRound
				&& !x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound)
				&& x.InputCount >= Config.RoundDestroyerThreshold).ToArray())
			{
				feeRate = await MiningFeeRateEstimator.GetRoundFeeRateAsync(cancellationToken).ConfigureAwait(false);

				var allInputs = round.Alices.Select(y => y.Coin.Amount).OrderBy(x => x).ToArray();

				// 0.75 to bias towards larger numbers as larger input owners often have many smaller inputs too.
				var smallSuggestion = allInputs.Skip((int)(allInputs.Length * Config.WW200CompatibleLoadBalancingInputSplit)).First();
				var largeSuggestion = MaxSuggestedAmountProvider.AbsoluteMaximumInput;

				var roundWithoutThis = Rounds.Except(new[] { round });
				RoundParameters parameters = RoundParameterFactory.CreateRoundParameter(feeRate, largeSuggestion);
				Round? foundLargeRound = roundWithoutThis
					.FirstOrDefault(x =>
									x.Phase == Phase.InputRegistration
									&& x is not BlameRound
									&& !x.IsInputRegistrationEnded(round.Parameters.MaxInputCountByRound)
									&& x.Parameters.MaxSuggestedAmount >= allInputs.Max()
									&& x.InputRegistrationTimeFrame.Remaining > TimeSpan.FromSeconds(60));
				var largeRound = foundLargeRound ?? TryMineRound(parameters, roundWithoutThis.ToArray());

				if (largeRound is not null)
				{
					parameters = RoundParameterFactory.CreateRoundParameter(feeRate, smallSuggestion);
					var smallRound = TryMineRound(parameters, roundWithoutThis.Concat(new[] { largeRound }).ToArray());

					// If creation is successful, only then destroy the round.
					if (smallRound is not null)
					{
						AddRound(largeRound);
						AddRound(smallRound);

						if (foundLargeRound is null)
						{
							largeRound.LogInfo($"Mined round with parameters: {nameof(largeRound.Parameters.MaxSuggestedAmount)}:'{largeRound.Parameters.MaxSuggestedAmount}' BTC.");
						}
						smallRound.LogInfo($"Mined round with parameters: {nameof(smallRound.Parameters.MaxSuggestedAmount)}:'{smallRound.Parameters.MaxSuggestedAmount}' BTC.");

						// If it can't create the large round, then don't abort.
						EndRound(round, EndRoundState.AbortedLoadBalancing);
						Logger.LogInfo($"Destroyed round with {allInputs.Length} inputs. Threshold: {Config.RoundDestroyerThreshold}");
					}
				}
			}
		}

		bool IsRoundRegistrable(TimeSpan inputRegRemainingTime, TimeSpan createNewRoundBeforeInputRegEnd)
		{
			var remainingTime = inputRegRemainingTime < TimeSpan.Zero ? TimeSpan.Zero : inputRegRemainingTime;
			return remainingTime >= Config.CreateNewRoundBeforeInputRegEnd;
		}

		// Add more rounds if not enough.
		var registrableRoundCount = Rounds.Count(
			x => x is not BlameRound &&
			x.Phase == Phase.InputRegistration &&
			IsRoundRegistrable(x.InputRegistrationTimeFrame.Remaining, Config.CreateNewRoundBeforeInputRegEnd));

		int roundsToCreate = Config.RoundParallelization - registrableRoundCount;
		for (int i = 0; i < roundsToCreate; i++)
		{
			feeRate ??= await MiningFeeRateEstimator.GetRoundFeeRateAsync(cancellationToken).ConfigureAwait(false);
			var r = CreateRoundObject(feeRate);
			AddRound(r);
			r.LogInfo($"Created round with parameters: {nameof(r.Parameters.MaxSuggestedAmount)}:'{r.Parameters.MaxSuggestedAmount}' BTC.");
		}
	}

	protected virtual Round CreateRoundObject(FeeRate feeRate)
	{
		RoundParameters parameters = RoundParameterFactory.CreateRoundParameter(feeRate, MaxSuggestedAmountProvider.MaxSuggestedAmount);

		var round = new Round(parameters, SecureRandom.Instance);
		return round;
	}

	private Round? TryMineRound(RoundParameters parameters, Round[] rounds)
	{
		// Huge HACK to keep it compatible with WW2.0.0 client version, which's
		// round preference is based on the ordering of ToImmutableDictionary.
		// Add round until ToImmutableDictionary orders it to be the first round
		// so old clients will prefer that one.
		IOrderedEnumerable<Round>? orderedRounds;
		Round r;
		var before = DateTimeOffset.UtcNow;
		var times = 0;
		var maxCycleTimes = 300;
		do
		{
			var roundsCopy = rounds.ToList();
			r = new Round(parameters, SecureRandom.Instance);
			roundsCopy.Add(r);
			orderedRounds = roundsCopy
				.Where(x => x.Phase == Phase.InputRegistration && x is not BlameRound && !x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound))
				.OrderBy(x => x.Parameters.MaxSuggestedAmount)
				.ThenBy(x => x.InputCount);
			times++;
		}
		while (times <= maxCycleTimes && orderedRounds.ToImmutableDictionary(x => x.Id, x => x).First().Key != r.Id);

		Logger.LogDebug($"First ordered round creator did {times} cycles.");

		if (times > maxCycleTimes)
		{
			r.LogInfo("First ordered round creation too expensive. Skipping...");
			return null;
		}
		else
		{
			return r;
		}
	}

	private void TimeoutRounds()
	{
		foreach (var expiredRound in Rounds.Where(
			x =>
			x.Phase == Phase.Ended
			&& x.End + Config.RoundExpiryTimeout < DateTimeOffset.UtcNow).ToArray())
		{
			Rounds.Remove(expiredRound);
		}
	}

	private void TimeoutAlices()
	{
		var now = DateTimeOffset.UtcNow;
		foreach (var round in Rounds.Where(x => !x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound)).ToArray())
		{
			var alicesToRemove = round.Alices.Where(x => x.Deadline < now && !x.ConfirmedConnection).ToArray();
			foreach (var alice in alicesToRemove)
			{
				round.Alices.Remove(alice);
				CoinVerifier?.CancelSchedule(alice.Coin);
			}

			var removedAliceCount = alicesToRemove.Length;
			if (removedAliceCount > 0)
			{
				round.LogInfo($"{removedAliceCount} alices timed out and removed.");
			}
		}
	}

	public virtual ConstructionState AddCoordinationFee(Round round, ConstructionState coinjoin, Script coordinatorScriptPubKey)
	{
		var sizeToPayFor = coinjoin.EstimatedVsize + coordinatorScriptPubKey.EstimateOutputVsize();
		var miningFee = round.Parameters.MiningFeeRate.GetFee(sizeToPayFor) + Money.Satoshis(1);

		round.ExpectedCoordinationFee = round.Alices.Where(a => !a.IsCoordinationFeeExempted).Sum(x => round.Parameters.CoordinationFeeRate.GetFee(x.Coin.Amount));
		var availableCoordinationFee = coinjoin.Balance - miningFee;

		round.LogInfo($"Expected coordination fee: {round.ExpectedCoordinationFee} - Available coordination: {availableCoordinationFee}.");

		if (availableCoordinationFee >= Config.MinFeeAmount)
		{
			coinjoin = coinjoin.AddFeeOutput(new TxOut(availableCoordinationFee, coordinatorScriptPubKey))
				.AsPayingForSharedOverhead();
			round.CoordinationFee = availableCoordinationFee;
		}
		else
		{
			round.CoordinationFee = 0L;
			round.LogWarning($"Available coordination fee wasn't taken, because it was too small: {availableCoordinationFee}.");
		}

		return coinjoin;
	}

	private Script GetCoordinatorScriptPreventReuse(Round round)
	{
		var coordinatorScriptPubKey = Config.GetNextCleanCoordinatorScript();

		// Prevent coordinator script reuse.
		if (Rounds.Any(r => r.CoordinatorScript == coordinatorScriptPubKey))
		{
			Config.MakeNextCoordinatorScriptDirty();
			coordinatorScriptPubKey = Config.GetNextCleanCoordinatorScript();
			round.LogWarning("Coordinator script pub key was already used by another round, making it dirty and taking a new one.");
		}

		return coordinatorScriptPubKey;
	}

	private void CoinVerifier_CoinBlacklisted(object? _, Coin coin)
	{
		// For logging reason Prison needs the roundId.
		var roundState = RoundStates.FirstOrDefault(rs => rs.CoinjoinState.Inputs.Any(input => input.Outpoint == coin.Outpoint));

		// Could be a coin from WW1.
		var roundId = roundState?.Id ?? uint256.Zero;
		Prison.FailedVerification(coin.Outpoint, roundId);
	}

	private void AddRound(Round round)
	{
		Rounds.Add(round);
		RoundCreated?.SafeInvoke(this, new RoundCreatedEventArgs(round.Id, round.Parameters));
	}

	public void AbortRound(uint256 roundId)
	{
		DisruptedRounds.Enqueue(roundId);
	}

	private void AbortDisruptedRounds()
	{
		while (DisruptedRounds.TryDequeue(out var disruptedRoundId))
		{
			var roundOrNull = Rounds.FirstOrDefault(x => x.Id == disruptedRoundId);
			if (roundOrNull is { } nonNullRound)
			{
				nonNullRound.LogInfo("Round aborted because it was disrupted by double spenders.");
				nonNullRound.EndRound(EndRoundState.AbortedDoubleSpendingDetected);
			}
		}
	}

	private void SetRoundPhase(Round round, Phase phase)
	{
		round.SetPhase(phase);

		if (phase == Phase.OutputRegistration)
		{
			foreach (Alice alice in round.Alices)
			{
				NotifyInput(round.Id, alice.Coin, alice.IsCoordinationFeeExempted);
			}
		}

		RoundPhaseChanged?.SafeInvoke(this, new RoundPhaseChangedEventArgs(round.Id, phase));
	}

	internal void EndRound(Round round, EndRoundState endRoundState)
	{
		round.EndRound(endRoundState);
		RoundPhaseChanged?.SafeInvoke(this, new RoundPhaseChangedEventArgs(round.Id, Phase.Ended));
	}

	private void NotifyInput(uint256 roundId, Coin coin, bool isCoordinationFeeExempted)
	{
		InputAdded.SafeInvoke(this, new InputAddedEventArgs(roundId, coin, isCoordinationFeeExempted));
	}

	private SigningState FinalizeTransaction(uint256 roundId, ConstructionState constructionState)
	{
		SigningState signingState = constructionState.Finalize();
		CoinJoinTransactionCreated?.SafeInvoke(this, new CoinJoinTransactionCreatedEventArgs(roundId, signingState.CreateTransaction()));
		return signingState;
	}

	public override void Dispose()
	{
		if (CoinVerifier is not null)
		{
			CoinVerifier.CoinBlacklisted -= CoinVerifier_CoinBlacklisted;
		}
		base.Dispose();
	}

	/// <summary>
	/// If too many inputs seem to misbehave, problem is probably on coordinator's side.
	/// Don't ban in that case to avoid huge amount of false-positives.
	/// </summary>
	private static bool ReasonableOffendersCount(int offendersCount, int minInputCount) => offendersCount <= minInputCount;
}
