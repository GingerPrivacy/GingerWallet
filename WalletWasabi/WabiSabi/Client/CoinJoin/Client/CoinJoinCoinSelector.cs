using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using SecureRandom = WabiSabi.Crypto.Randomness.SecureRandom;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public class CoinJoinCoinSelector
{
	public CoinJoinCoinSelector(IWallet wallet, WasabiRandom? random = null) : this(wallet.ConsolidationMode, wallet.AnonScoreTarget, wallet.RedCoinIsolation ? Constants.SemiPrivateThreshold : 0, null, wallet, random)
	{
	}

	/// <param name="consolidationMode">If true it attempts to select as many coins as it can.</param>
	/// <param name="anonScoreTarget">Tries to select few coins over this threshold.</param>
	/// <param name="semiPrivateThreshold">Minimum anonymity of coins that can be selected together.</param>
	public CoinJoinCoinSelector(bool consolidationMode, int anonScoreTarget, int semiPrivateThreshold, CoinJoinCoinSelectorRandomnessGenerator? generator = null, IWallet? wallet = null, WasabiRandom? random = null)
	{
		_wallet = wallet;
		_random = random ?? SecureRandom.Instance;
		_settings = _wallet?.CoinJoinCoinSelectionSettings ?? new();
		_comparer = new(10.0, _settings.WeightedAnonymityLossNormal, _settings.ValueLossRateNormal);

		ConsolidationMode = consolidationMode;
		AnonScoreTarget = anonScoreTarget;
		SemiPrivateThreshold = semiPrivateThreshold;

		Generator = generator ?? new(MaxInputsRegistrableByWallet, SecureRandom.Instance);
	}

	private IWallet? _wallet = null;
	private CoinCollectionStatistics? _walletStatistics = null;
	private CoinSelectionStatisticsComparer _comparer;
	private CoinJoinCoinSelectionSettings _settings;

	public async Task CheckWalletAsync()
	{
		if (_wallet != null)
		{
			// In reality this should be all the spendable coins, any meaningful filtering will come after getting this enumerable
			var walletCoins = new CoinsView(await _wallet.GetCoinjoinCoinCandidatesAsync().ConfigureAwait(false)).ToList();
			_walletStatistics = new(walletCoins, _settings.TargetCoinCountPerBucket);
		}
		else
		{
			_walletStatistics = new([], _settings.TargetCoinCountPerBucket);
		}
	}

	internal static int GetBucketIndex(SmartCoin coin)
	{
		long amount = coin.Amount.Satoshi;
		return amount > 0 ? 64 - BitOperations.LeadingZeroCount(((ulong)amount) / 5000) : -1;
	}

	private const double AvgInputsRegistrableByWallet = 10.0;
	private const int MaxInputsRegistrableByWalletForNewCoinSelector = 12;

	private CoinJoinCoinSelectionParameters CreateParameters(UtxoSelectionParameters parameters, double maxWeightedAnonymityLoss, double maxValueLossRate)
	{
		CoinJoinCoinSelectionParameters res = new(
			AnonScoreTarget: AnonScoreTarget,
			MiningFeeRate: parameters.MiningFeeRate,
			MinInputAmount: parameters.AllowedInputAmounts.Min.Satoshi,
			CoinJoinLoss: (long)(.4 * parameters.MinAllowedOutputAmount.Satoshi + parameters.MiningFeeRate.GetFee(8 * Constants.P2wpkhOutputVirtualSize).Satoshi),
			MaxValueLossRate: maxValueLossRate,
			MaxCoinLossRate: 0.035,
			MaxWeightedAnonymityLoss: maxWeightedAnonymityLoss,
			WalletStatistics: _walletStatistics ?? new([], 0),
			Comparer: _comparer,
			Random: _random
			);
		return res;
	}

	private CoinJoinCoinSelectionParameters _coinSelectionParameters = CoinJoinCoinSelectionParameters.Empty;

	private const int SufficeCandidateCountForCoinSelection = 200;

	public List<SmartCoin> SelectCoinsForRoundNew(UtxoSelectionParameters parameters, Money liquidityClue)
	{
		// We don't support red/isolated coins
		if (_redCoins.Count > 0 || _wallet is null || _walletStatistics is null)
		{
			_wallet?.LogInfo($"Red coin isolation is not supported by the new coin selector.");
			return [];
		}

		_coinSelectionParameters = CreateParameters(parameters, 20.0, 0.1);
		List<SmartCoin> selectableCoins = _semiPrivateCoins.Where(x => !_coinSelectionParameters.IsCoinAboveAllowedLoss(x)).ToList();
		_wallet.LogInfo($"Semi-private coins that can be selected without to high value loss: {selectableCoins.Count}");
		if (selectableCoins.Count == 0)
		{
			_wallet.LogInfo($"No selectable semi-private coin without too much loss.");
			return [];
		}

		var amountScale = -1.0 / selectableCoins.Select(x => x.Amount.Satoshi).Max();
		selectableCoins = selectableCoins.OrderBy(x => Math.Round(x.AnonymitySet, 3) * 1000 + x.Amount.Satoshi * amountScale).ToList();

		// if we are forced to use low privacy coins, we will try more possibilities since we will have less starting coins
		bool forceUsingLowPrivacyCoins = _settings.ForceUsingLowPrivacyCoins;
		var candidateCoins = selectableCoins;
		if (forceUsingLowPrivacyCoins)
		{
			double maxAnonymity = candidateCoins[0].AnonymitySet + 0.5;
			candidateCoins = candidateCoins.Where(x => x.AnonymitySet < maxAnonymity).ToList();
		}

		Dictionary<SmartCoin, List<CoinSelectionCandidate>> candidatesDict = new();
		foreach (SmartCoin coin in candidateCoins)
		{
			candidatesDict.Add(coin, new());
		}

		// First we need to find the target input count
		// Calculate the total coin number we should minimum have (using TargetCoinNumberPerBucket pieces 5k, 10k, 20k etc. coins to have the actual wallet amount)
		int startingBucket = Math.Max(_coinSelectionParameters.StartingBucketIndex, 1);
		int walletCoinCount = _walletStatistics.GetCoinCountFromBucketIndex(startingBucket);
		long walletAmount = _walletStatistics.GetAmountFromBucketIndex(startingBucket).Satoshi;
		double walletMaxBallance = _walletStatistics.BucketBalance.Count > startingBucket ? _walletStatistics.BucketBalance.Skip(startingBucket).Max() : 10;
		// Average bucket value is Sum(3750, 7500 .. etc. xCoinNumberPerBucket) = ActualValue, solving the equation gives the below formula
		double expectedCoinCount = _settings.TargetCoinCountPerBucket * (Math.Log2(walletAmount / (3750.0 * _settings.TargetCoinCountPerBucket) + Math.Pow(2, startingBucket)) - startingBucket);
		double coinCountRate = walletCoinCount / expectedCoinCount;

		const double RateRange = 0.3;
		int inputCount = (int)Math.Round((1 + Math.Min(Math.Max((coinCountRate - 1) * RateRange, -RateRange), RateRange) + (_random.GetInt(-120, 120) * 0.001)) * AvgInputsRegistrableByWallet);
		inputCount = Math.Min(Math.Min(inputCount, MaxInputsRegistrableByWalletForNewCoinSelector), _semiPrivateCoins.Count);

		_wallet.LogInfo($"Target InputCount is {inputCount} (Amount {walletAmount}, CoinRate {coinCountRate:F2} = {walletCoinCount} / {expectedCoinCount:F2})");

		// Always try to get candidates without private coins first
		CollectCandidates(candidatesDict, selectableCoins, inputCount, walletMaxBallance, parameters);

		if (_settings.CanSelectPrivateCoins)
		{
			int candidateCountNonPrivate = candidatesDict.Sum(x => x.Value.Count);
			if (candidateCountNonPrivate >= SufficeCandidateCountForCoinSelection)
			{
				_wallet.LogInfo($"Using private coins is allowed, but already have enough candidates {candidateCountNonPrivate}, not using private coins.");
			}
			else
			{
				List<SmartCoin> selectablePrivateCoins = _privateCoins.Where(x => !_coinSelectionParameters.IsCoinAboveAllowedLoss(x)).ToList();
				_wallet.LogInfo($"Using private coins is allowed, {selectablePrivateCoins.Count} coins found.");
				if (selectablePrivateCoins.Count > 0)
				{
					selectablePrivateCoins = selectablePrivateCoins.OrderBy(x => Math.Round(x.AnonymitySet, 3) * 1000 + x.Amount.Satoshi * amountScale).ToList();
					// Simply adding to the end and retry
					selectableCoins.AddRange(selectablePrivateCoins);
					CollectCandidates(candidatesDict, selectableCoins, inputCount, walletMaxBallance, parameters);
					int candidateCountPrivate = candidatesDict.Sum(x => x.Value.Count);
					_wallet.LogInfo($"Changed the possible candidates from {candidateCountNonPrivate} to {candidateCountPrivate}.");
				}
			}
		}

		List<CoinSelectionCandidate> candidates = new();
		foreach (var candidateList in candidatesDict.Values)
		{
			candidates.AddRange(candidateList);
		}
		if (candidates.Count > 0)
		{
			candidates.Sort(_coinSelectionParameters.Comparer);
			for (int idx = candidates.Count - 2; idx >= 0; idx--)
			{
				if (candidates[idx].Equals(candidates[idx + 1]))
				{
					candidates.RemoveAt(idx + 1);
				}
			}
			int unqiueCandidatesCount = candidates.Count;
			// Now we need to choose
			var bestScore = candidates[0].Score;
			var bestLossScore = _comparer.GetLossScore(candidates[0]);
			var scoreLimit = bestScore + 0.3 * bestLossScore;
			for (int idx = 0, len = candidates.Count; idx < len; idx++)
			{
				if (candidates[idx].Score > scoreLimit)
				{
					candidates.RemoveRange(idx, candidates.Count - idx);
					break;
				}
			}
			_wallet.LogInfo($"Created {unqiueCandidatesCount} candidates, kept the first {candidates.Count} with scores {bestScore} - {scoreLimit} to choose from.");

			var finalCandidate = candidates.RandomElement(_random) ?? candidates[0];
			return finalCandidate.Coins.ToShuffled(_random).ToList();
		}

		return [];
	}

	private void CollectCandidates(Dictionary<SmartCoin, List<CoinSelectionCandidate>> candidates, List<SmartCoin> selectableCoins, int inputCount, double walletMaxBallance, UtxoSelectionParameters parameters)
	{
		int sensitivityDecrement = 2;
		int absLowestSensitivity = (int)-Math.Min(Math.Ceiling(walletMaxBallance), inputCount) - sensitivityDecrement;

		bool forceUsingLowPrivacyCoins = _settings.ForceUsingLowPrivacyCoins;
		int maxDistance = forceUsingLowPrivacyCoins ? 30 : 20;
		double valueRateLossMul = 0.025 / maxDistance;
		double anonymityLossMul = (forceUsingLowPrivacyCoins ? 0.5 : 1.0) * Math.Max(AnonScoreTarget - 1.5, 4.0) / maxDistance;
		int totalCandidates = candidates.Sum(x => x.Value.Count);
		for (int lowestSensitivity = 1; lowestSensitivity >= absLowestSensitivity && totalCandidates < SufficeCandidateCountForCoinSelection; lowestSensitivity -= sensitivityDecrement)
		{
			int distanceIncrement = 1;
			for (int idx = 0; idx < maxDistance && totalCandidates < SufficeCandidateCountForCoinSelection; idx += distanceIncrement)
			{
				for (int jdx = 0; jdx < idx; jdx++)
				{
					double maxValueRateLoss = 0.001 + jdx * valueRateLossMul;
					double maxWeightedAnonymityLoss = 1.5 + (idx - jdx) * anonymityLossMul;
					_coinSelectionParameters = CreateParameters(parameters, maxWeightedAnonymityLoss, maxValueRateLoss);
					CollectCandidatesWithSensitivity(candidates, selectableCoins, inputCount, lowestSensitivity);
				}
				int recountedCandidates = candidates.Sum(x => x.Value.Count);
				if (recountedCandidates > 0 && recountedCandidates - totalCandidates < 2 && idx + 1 < maxDistance)
				{
					distanceIncrement = Math.Min(distanceIncrement + 1, maxDistance - idx - 1);
				}
				else
				{
					distanceIncrement = 1;
				}
				totalCandidates = recountedCandidates;
			}
		}
	}

	private void CollectCandidatesWithSensitivity(Dictionary<SmartCoin, List<CoinSelectionCandidate>> candidates, List<SmartCoin> selectableCoins, int inputCount, double lowestSensitivity)
	{
		bool forceUsingLowPrivacyCoins = _settings.ForceUsingLowPrivacyCoins;
		double coinCheck = SufficeCandidateCountForCoinSelection / (double)candidates.Count;
		int triesPerStartingCoin = Math.Max(2, Math.Min((int)Math.Round(coinCheck), 6));
		int maxCandidatesPerStartingCoin = Math.Max(3, Math.Min((int)Math.Round(coinCheck * 2), 20));
		// If we have very few starting coins then we try to increase the allowed candidates per coin further
		if (candidates.Count < 4)
		{
			maxCandidatesPerStartingCoin = Math.Max(maxCandidatesPerStartingCoin, (int)Math.Round(coinCheck));
		}

		// These are the starting candidates
		foreach (var (coin, list) in candidates)
		{
			if (list.Count >= maxCandidatesPerStartingCoin)
			{
				continue;
			}

			var coinList = GetAnonContinuityList(selectableCoins, coin, _coinSelectionParameters.MaxWeightedAnonymityLoss);
			if (coinList.Count == 0)
			{
				continue;
			}
			if (list.Count < maxCandidatesPerStartingCoin)
			{
				CoinSelectionCandidate cscStart = new(coinList, _coinSelectionParameters);
				for (int tidx = triesPerStartingCoin; tidx > 0 && list.Count < maxCandidatesPerStartingCoin; tidx--)
				{
					CoinSelectionCandidate csc = tidx > 1 ? new(cscStart) : cscStart;
					if (csc.MeetTheRequirements(inputCount, lowestSensitivity) && list.All(x => !x.Equals(csc)))
					{
						list.Add(csc);
					}
				}
			}
		}
	}

	// We give back a continous list tile the MaxGroupAnonLoss and the MaxValueLoss is met
	private List<SmartCoin> GetAnonContinuityList(List<SmartCoin> list, SmartCoin startingCoin, double maxWeightedAnonymityLoss)
	{
		List<SmartCoin> result = new();
		int idx = list.IndexOf(startingCoin);
		if (idx >= 0)
		{
			long sumAmount = 0;
			int sumVSize = 0;
			double minimumAnonScore = startingCoin.AnonymitySet;
			double sumAmountMulAnonScore = 0;

			for (; idx < list.Count; idx++)
			{
				SmartCoin coin = list[idx];
				sumAmount += coin.Amount.Satoshi;
				sumVSize += coin.ScriptPubKey.EstimateInputVsize();
				sumAmountMulAnonScore += CoinSelectionCandidate.GetCoinAnonymityWeight(coin, AnonScoreTarget, minimumAnonScore);
				if (sumAmountMulAnonScore <= maxWeightedAnonymityLoss * sumAmount)
				{
					result.Add(coin);
				}
			}
		}

		return result;
	}

	// The coin selection process is all over different classes, so there is not a single class that knows what it is actually doing

	public const int MaxInputsRegistrableByWallet = 10; // how many
	public const int MaxWeightedAnonLoss = 3; // Maximum tolerable WeightedAnonLoss.

	public bool ConsolidationMode { get; }
	public int AnonScoreTarget { get; }
	public int SemiPrivateThreshold { get; }
	private WasabiRandom Rnd => Generator.Rnd;
	private CoinJoinCoinSelectorRandomnessGenerator Generator { get; }

	private WasabiRandom _random = SecureRandom.Instance;
	private List<SmartCoin> _filteredCoins = new();
	private List<SmartCoin> _privateCoins = new();
	private List<SmartCoin> _semiPrivateCoins = new();
	private List<SmartCoin> _redCoins = new();

	public async Task<bool> StartCoinSelectionAsync(IEnumerable<SmartCoin> coins, UtxoSelectionParameters parameters)
	{
		_filteredCoins = coins
			.Where(x => parameters.AllowedInputAmounts.Contains(x.Amount))
			.Where(x => parameters.AllowedInputScriptTypes.Contains(x.ScriptType))
			.Where(x => x.EffectiveValue(parameters.MiningFeeRate) > Money.Zero)
			.ToList();

		// Sanity check.
		if (_filteredCoins.Count == 0)
		{
			_wallet?.LogInfo("No suitable coins for this round.");
			return false;
		}

		await CheckWalletAsync().ConfigureAwait(false);

		_privateCoins = _filteredCoins.Where(x => x.IsPrivate(AnonScoreTarget)).ToList();
		_semiPrivateCoins = _filteredCoins.Where(x => x.IsSemiPrivate(AnonScoreTarget, SemiPrivateThreshold)).ToList();

		// redCoins will only fill up if redCoinIsolation is turned on. Otherwise the coin will be in semiPrivateCoins.
		_redCoins = _filteredCoins.Where(x => x.IsRedCoin(SemiPrivateThreshold)).ToList();

		if (_semiPrivateCoins.Count + _redCoins.Count == 0)
		{
			_wallet?.LogInfo("No suitable coins for this round.");
			return false;
		}

		_wallet?.LogInfo($"Coin selection started:");
		_wallet?.LogInfo($"Filtered Coins: {_filteredCoins.Count} coins, valued at {Money.Satoshis(_filteredCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");
		_wallet?.LogInfo($"Private Coins: {_privateCoins.Count} coins, valued at {Money.Satoshis(_privateCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");
		_wallet?.LogInfo($"SemiPrivate Coins: {_semiPrivateCoins.Count} coins, valued at {Money.Satoshis(_semiPrivateCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");
		_wallet?.LogInfo($"Red Coins: {_redCoins.Count} coins, valued at {Money.Satoshis(_redCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");
		_wallet?.LogInfo($"FeeRate: {parameters.MiningFeeRate}");

		return true;
	}

	/// <param name="liquidityClue">Weakly prefer not to select inputs over this.</param>
	public async Task<ImmutableList<SmartCoin>> SelectCoinsForRoundAsync(IEnumerable<SmartCoin> coins, UtxoSelectionParameters parameters, Money liquidityClue)
	{
		if (!await StartCoinSelectionAsync(coins, parameters).ConfigureAwait(false))
		{
			return [];
		}

		CoinJoinCoinSelectionSettings settings = _wallet?.CoinJoinCoinSelectionSettings ?? new();

		liquidityClue = liquidityClue > Money.Zero ? liquidityClue : Constants.MaximumNumberOfBitcoinsMoney;

		DateTime time = DateTime.UtcNow;
		List<SmartCoin> newResult = settings.UseExperimentalCoinSelector ? SelectCoinsForRoundNew(parameters, liquidityClue) : [];
		TimeSpan stopperNew = DateTime.UtcNow - time;
		bool useOldSelector = !settings.UseExperimentalCoinSelector || settings.UseOldCoinSelectorAsFallback;
		time = DateTime.UtcNow;
		List<SmartCoin> oldResult = useOldSelector ? SelectCoinsForRoundOld(parameters, liquidityClue) : [];
		TimeSpan stopperOld = DateTime.UtcNow - time;

		// Only for logging the comparison
		_coinSelectionParameters = CreateParameters(parameters, 3.0, 0.005);
		CoinSelectionCandidate candidateNew = new(newResult, _coinSelectionParameters);
		CoinSelectionCandidate candidateOld = new(oldResult, _coinSelectionParameters);

		_wallet?.LogInfo("CoinSelection stats: [final score, lower is better] ([amount], [coin count], [transaction count]), ([anonymity difference between coins], [estimated valus loss rate], [bucket score]), [coins' lowest anonymity]");
		if (settings.UseExperimentalCoinSelector)
		{
			_wallet?.LogInfo($"Coin selection candidate result by the new algorithm ({stopperNew.TotalSeconds:F2}sec): {candidateNew}");
		}
		if (useOldSelector)
		{
			_wallet?.LogInfo($"Coin selection candidate result by the old algorithm ({stopperOld.TotalSeconds:F2}sec): {candidateOld}");
		}
		if (newResult.Count > 0 && (candidateNew.Score <= candidateOld.Score || candidateNew.CoinCount != candidateOld.CoinCount))
		{
			_wallet?.LogInfo("Choosing the candidate given by the new algorithm.");
			return newResult.ToImmutableList();
		}
		if (oldResult.Count > 0)
		{
			_wallet?.LogInfo("Choosing the candidate given by the old algorithm.");
			return oldResult.ToImmutableList();
		}
		_wallet?.LogInfo("The algorithms were not able to give a valid coin list for the coinjoin.");
		return [];
	}

	public List<SmartCoin> SelectCoinsForRoundOld(UtxoSelectionParameters parameters, Money liquidityClue)
	{
		// This step has many problematic parts.
		// We randomly choose one red coin.
		// 1. This means we choose a coin that can generate a huge annon loss without forcing anything to the rest of the process.
		// 2. The calculated anon loss (check GetAnonLoss) is independent from the amount of the red coin.
		// 3. Not considering the mining fee and the amount of the red coin we choose is probably not a wise decision either.
		// Because of the above we are most likely drop the red coin altogether by the end of the below process if its amount is not in the top range of the coins.

		// We want to isolate red coins from each other. We only let a single red coin get into our selection candidates.
		List<SmartCoin> allowedNonPrivateCoins = new(_semiPrivateCoins);
		var red = _redCoins.RandomElement(Rnd);
		if (red is not null)
		{
			allowedNonPrivateCoins.Add(red);
			Logger.LogDebug($"One red coin got selected: {red.Amount.ToString(false, true)} BTC. Isolating the rest.");
		}

		Logger.LogDebug($"{nameof(allowedNonPrivateCoins)}: {allowedNonPrivateCoins.Count} coins, valued at {Money.Satoshis(allowedNonPrivateCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");

		int inputCount = Math.Min(
			_privateCoins.Count + allowedNonPrivateCoins.Count,
			ConsolidationMode ? MaxInputsRegistrableByWallet : Generator.GetInputTarget());
		if (ConsolidationMode)
		{
			Logger.LogDebug($"Consolidation mode is on.");
		}
		Logger.LogDebug($"Targeted {nameof(inputCount)}: {inputCount}.");

		var biasShuffledPrivateCoins = AnonScoreTxSourceBiasedShuffle(_privateCoins).ToArray();

		// Deprioritize private coins those are too large.
		var smallerPrivateCoins = biasShuffledPrivateCoins.Where(x => x.Amount <= liquidityClue);
		var largerPrivateCoins = biasShuffledPrivateCoins.Where(x => x.Amount > liquidityClue);

		// Let's allow only inputCount - 1 private coins to play.
		var allowedPrivateCoins = smallerPrivateCoins.Concat(largerPrivateCoins).Take(inputCount - 1).ToArray();
		Logger.LogDebug($"{nameof(allowedPrivateCoins)}: {allowedPrivateCoins.Length} coins, valued at {Money.Satoshis(allowedPrivateCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");

		var allowedCoins = allowedNonPrivateCoins.Concat(allowedPrivateCoins).ToList();
		Logger.LogDebug($"{nameof(allowedCoins)}: {allowedCoins.Count} coins, valued at {Money.Satoshis(allowedCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");

		// Shuffle coins, while randomly biasing towards lower AS.
		var orderedAllowedCoins = AnonScoreTxSourceBiasedShuffle(allowedCoins).ToArray();

		// Always use the largest amounts, so we do not participate with insignificant amounts and fragment wallet needlessly.
		var largestNonPrivateCoins = allowedNonPrivateCoins
			.OrderByDescending(x => x.Amount)
			.Take(3)
			.ToArray();
		Logger.LogDebug($"Largest non-private coins: {string.Join(", ", largestNonPrivateCoins.Select(x => x.Amount.ToString(false, true)).ToArray())} BTC.");

		// Select a group of coins those are close to each other by anonymity score.
		Dictionary<int, IEnumerable<SmartCoin>> groups = new();

		// Create a bunch of combinations.
		var sw1 = Stopwatch.StartNew();
		foreach (var coin in largestNonPrivateCoins)
		{
			// Create a base combination just in case.
			var baseGroup = orderedAllowedCoins.Except(new[] { coin }).Take(inputCount - 1).Concat(new[] { coin });
			TryAddGroup(parameters, groups, baseGroup);

			var sw2 = Stopwatch.StartNew();
			foreach (var group in orderedAllowedCoins
				.Except(new[] { coin })
				.CombinationsWithoutRepetition(inputCount - 1)
				.Select(x => x.Concat(new[] { coin })))
			{
				TryAddGroup(parameters, groups, group);

				if (sw2.Elapsed > TimeSpan.FromSeconds(1))
				{
					break;
				}
			}

			sw2.Reset();

			if (sw1.Elapsed > TimeSpan.FromSeconds(10))
			{
				break;
			}
		}

		if (groups.Count == 0)
		{
			Logger.LogDebug($"Couldn't create any combinations, ending.");
			return [];
		}
		Logger.LogDebug($"Created {groups.Count} combinations within {(int)sw1.Elapsed.TotalSeconds} seconds.");

		// Select the group where the less coins coming from the same tx.
		var bestRep = groups.Values.Select(x => GetReps(x)).Min(x => x);
		var bestRepGroups = groups.Values.Where(x => GetReps(x) == bestRep);
		Logger.LogDebug($"{nameof(bestRep)}: {bestRep}.");
		Logger.LogDebug($"Filtered combinations down to {nameof(bestRepGroups)}: {bestRepGroups.Count()}.");

		var remainingLargestNonPrivateCoins = largestNonPrivateCoins.Where(x => bestRepGroups.Any(y => y.Contains(x)));
		Logger.LogDebug($"Remaining largest non-private coins: {string.Join(", ", remainingLargestNonPrivateCoins.Select(x => x.Amount.ToString(false, true)).ToArray())} BTC.");

		// Bias selection towards larger numbers.
		var selectedNonPrivateCoin = remainingLargestNonPrivateCoins.RandomElement(Rnd); // Select randomly at first just to have a starting value.
		foreach (var coin in remainingLargestNonPrivateCoins.OrderByDescending(x => x.Amount))
		{
			if (Rnd.GetInt(1, 101) <= 50)
			{
				selectedNonPrivateCoin = coin;
				break;
			}
		}
		if (selectedNonPrivateCoin is null)
		{
			Logger.LogDebug($"Couldn't select largest non-private coin, ending.");
			return [];
		}
		Logger.LogDebug($"Randomly selected large non-private coin: {selectedNonPrivateCoin.Amount.ToString(false, true)}.");

		var finalCandidate = bestRepGroups
			.Where(x => x.Contains(selectedNonPrivateCoin))
			.RandomElement(Rnd);
		if (finalCandidate is null)
		{
			Logger.LogDebug($"Couldn't select final selection candidate, ending.");
			return [];
		}
		Logger.LogDebug($"Selected the final selection candidate: {finalCandidate.Count()} coins, {string.Join(", ", finalCandidate.Select(x => x.Amount.ToString(false, true)).ToArray())} BTC.");

		// Let's remove some coins coming from the same tx in the final candidate:
		// The smaller our balance is the more privacy we gain and the more the user cares about the costs, so more interconnectedness allowance makes sense.
		var toRegister = finalCandidate.Sum(x => x.Amount);
		int percent;
		if (toRegister < 10_000)
		{
			percent = 20;
		}
		else if (toRegister < 100_000)
		{
			percent = 30;
		}
		else if (toRegister < 1_000_000)
		{
			percent = 40;
		}
		else if (toRegister < 10_000_000)
		{
			percent = 50;
		}
		else if (toRegister < 100_000_000) // 1 BTC
		{
			percent = 60;
		}
		else if (toRegister < 1_000_000_000)
		{
			percent = 70;
		}
		else
		{
			percent = 80;
		}

		int sameTxAllowance = Generator.GetRandomBiasedSameTxAllowance(percent);

		List<SmartCoin> winner = new()
		{
			selectedNonPrivateCoin
		};

		foreach (var coin in finalCandidate
			.Except(new[] { selectedNonPrivateCoin })
			.OrderBy(x => x.AnonymitySet)
			.ThenByDescending(x => x.Amount))
		{
			// If the coin is coming from same tx, then check our allowance.
			if (winner.Any(x => x.TransactionId == coin.TransactionId))
			{
				var sameTxUsed = winner.Count - winner.Select(x => x.TransactionId).Distinct().Count();
				if (sameTxUsed < sameTxAllowance)
				{
					winner.Add(coin);
				}
			}
			else
			{
				winner.Add(coin);
			}
		}

		double winnerAnonLoss = GetAnonLoss(winner);

		// Only stay in the while if we are above the liquidityClue (we are a whale) AND the weightedAnonLoss is not tolerable.
		while (winner.Sum(x => x.Amount) > liquidityClue && winnerAnonLoss > MaxWeightedAnonLoss)
		{
			List<SmartCoin> bestReducedWinner = winner;
			var bestAnonLoss = winnerAnonLoss;
			bool winnerChanged = false;

			// We always want to keep the non-private coins.
			foreach (var coin in winner.Except(new[] { selectedNonPrivateCoin }))
			{
				var reducedWinner = winner.Except(new[] { coin });
				var anonLoss = GetAnonLoss(reducedWinner);

				if (anonLoss <= bestAnonLoss)
				{
					bestAnonLoss = anonLoss;
					bestReducedWinner = reducedWinner.ToList();
					winnerChanged = true;
				}
			}

			if (!winnerChanged)
			{
				break;
			}

			winner = bestReducedWinner;
			winnerAnonLoss = bestAnonLoss;
		}

		if (winner.Count != finalCandidate.Count())
		{
			Logger.LogDebug($"Optimizing selection, removing coins coming from the same tx.");
			Logger.LogDebug($"{nameof(sameTxAllowance)}: {sameTxAllowance}.");
			Logger.LogDebug($"{nameof(winner)}: {winner.Count} coins, {string.Join(", ", winner.Select(x => x.Amount.ToString(false, true)).ToArray())} BTC.");
		}

		if (winner.Count < MaxInputsRegistrableByWallet)
		{
			// If the address of a winner contains other coins (address reuse, same HdPubKey) that are available but not selected,
			// complete the selection with them until MaxInputsRegistrableByWallet threshold.
			// Order by most to least reused to try not splitting coins from same address into several rounds.
			var nonSelectedCoinsOnSameAddresses = _filteredCoins
				.Except(winner)
				.Where(x => winner.Any(y => y.ScriptPubKey == x.ScriptPubKey))
				.GroupBy(x => x.ScriptPubKey)
				.OrderByDescending(g => g.Count())
				.SelectMany(g => g)
				.Take(MaxInputsRegistrableByWallet - winner.Count)
				.ToList();

			winner.AddRange(nonSelectedCoinsOnSameAddresses);

			if (nonSelectedCoinsOnSameAddresses.Count > 0)
			{
				Logger.LogInfo($"{nonSelectedCoinsOnSameAddresses.Count} coins were added to the selection because they are on the same addresses of some selected coins.");
			}
		}

		return winner.ToShuffled(Rnd).ToList();
	}

	private IEnumerable<SmartCoin> AnonScoreTxSourceBiasedShuffle(List<SmartCoin> coins)
	{
		var orderedCoins = new List<SmartCoin>();
		for (int i = 0; i < coins.Count; i++)
		{
			// Order by anonscore first.
			var remaining = coins.Except(orderedCoins).OrderBy(x => x.AnonymitySet);

			// Then manipulate the list so repeating tx sources go to the end.
			var alternating = new List<SmartCoin>();
			var skipped = new List<SmartCoin>();
			foreach (var c in remaining)
			{
				if (alternating.Any(x => x.TransactionId == c.TransactionId) || orderedCoins.Any(x => x.TransactionId == c.TransactionId))
				{
					skipped.Add(c);
				}
				else
				{
					alternating.Add(c);
				}
			}
			alternating.AddRange(skipped);

			var coin = alternating.BiasedRandomElement(biasPercent: 50, Rnd)!;
			orderedCoins.Add(coin);
			yield return coin;
		}
	}

	private static bool TryAddGroup<TCoin>(UtxoSelectionParameters parameters, Dictionary<int, IEnumerable<TCoin>> groups, IEnumerable<TCoin> group)
		where TCoin : ISmartCoin
	{
		var effectiveInputSum = group.Sum(x => x.EffectiveValue(parameters.MiningFeeRate, parameters.CoordinationFeeRate));
		if (effectiveInputSum >= parameters.MinAllowedOutputAmount)
		{
			var k = HashCode.Combine(group.OrderBy(x => x.TransactionId).ThenBy(x => x.Index));
			return groups.TryAdd(k, group);
		}

		return false;
	}

	private static double GetAnonLoss<TCoin>(IEnumerable<TCoin> coins)
		where TCoin : ISmartCoin
	{
		double minimumAnonScore = coins.Min(x => x.AnonymitySet);
		return coins.Sum(x => (x.AnonymitySet - minimumAnonScore) * x.Amount.Satoshi) / coins.Sum(x => x.Amount.Satoshi);
	}

	// This is an obfuscated way to calculate group.Count - transactions.Count !!!
	private static int GetReps<TCoin>(IEnumerable<TCoin> group)
		where TCoin : ISmartCoin
		=> group.GroupBy(x => x.TransactionId).Sum(coinsInTxGroup => coinsInTxGroup.Count() - 1);
}
