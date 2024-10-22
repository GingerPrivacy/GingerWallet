using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Bases;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.RoundStateAwaiters;

public class RoundStateUpdater : PeriodicRunner
{
	public RoundStateUpdater(TimeSpan requestInterval, IWabiSabiApiRequestHandler arenaRequestHandler, bool verifyRoundState = true) : base(requestInterval)
	{
		ArenaRequestHandler = arenaRequestHandler;
		// To turn off the RoundState verify code for simple tests
		_verifyRoundState = verifyRoundState;
	}

	private IWabiSabiApiRequestHandler ArenaRequestHandler { get; }
	private Dictionary<uint256, RoundStateHolder> RoundStates { get; set; } = new();
	public Dictionary<TimeSpan, FeeRate> CoinJoinFeeRateMedians { get; private set; } = new();

	private List<RoundStateAwaiter> Awaiters { get; } = new();
	private object AwaitersLock { get; } = new();

	public bool AnyRound => RoundStates.Any();

	public bool SlowRequestsMode { get; set; } = true;

	private DateTimeOffset LastSuccessfulRequestTime { get; set; }

	private bool _verifyRoundState;
	private WasabiRandom _random = SecureRandom.Instance;

	private DateTimeOffset _lastRequestTime;
	private TimeSpan _waitSlowRequestMode = TimeSpan.Zero;
	private TimeSpan _waitPeriod = TimeSpan.Zero;

	public async Task ForceTriggerAndWaitRoundAsync(TimeSpan timeout)
	{
		_waitPeriod = TimeSpan.Zero;
		_waitSlowRequestMode = TimeSpan.Zero;
		await TriggerAndWaitRoundAsync(timeout).ConfigureAwait(false);
	}

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		if (DateTimeOffset.UtcNow - _lastRequestTime < _waitPeriod)
		{
			return;
		}

		if (SlowRequestsMode)
		{
			lock (AwaitersLock)
			{
				if (Awaiters.Count == 0 && DateTimeOffset.UtcNow - LastSuccessfulRequestTime < _waitSlowRequestMode)
				{
					return;
				}
			}
		}

		_lastRequestTime = DateTimeOffset.UtcNow;
		_waitPeriod = TimeSpan.FromMilliseconds(_random.GetInt(4000, 10000));

		// Randomly rerequest the full RoundState to increase the confidence
		var requestFromCheckpointList = RoundStates.Where(x => x.Value.Confidence > 2 && _random.GetInt(0, 100) < 70).ToDictionary();
		var request = new RoundStateRequest(requestFromCheckpointList.Select(x => new RoundStateCheckpoint(x.Key, x.Value.RoundState.CoinjoinState.Events.Count)).ToImmutableList());

		using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(30));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		var response = await ArenaRequestHandler.GetStatusAsync(request, linkedCts.Token).ConfigureAwait(false);

		CoinJoinFeeRateMedians = response.CoinJoinFeeRateMedians.ToDictionary(a => a.TimeFrame, a => a.MedianFeeRate);

		// Don't use ToImmutable dictionary, because that ruins the original order and makes the server unable to suggest a round preference.
		// ToDo: ToDictionary doesn't guarantee the order by design so .NET team might change this out of our feet, so there's room for improvement here.
		RoundStates = response.RoundStates.Select(rs => CheckAndMergeRoundState(rs, requestFromCheckpointList)).ToDictionary(x => x.RoundState.Id, x => x);

		lock (AwaitersLock)
		{
			foreach (var awaiter in Awaiters.Where(awaiter => awaiter.IsCompleted(RoundStates)).ToArray())
			{
				// The predicate was fulfilled.
				Awaiters.Remove(awaiter);
				break;
			}
		}

		LastSuccessfulRequestTime = _lastRequestTime = DateTimeOffset.UtcNow;
		_waitSlowRequestMode = TimeSpan.FromMilliseconds(_random.GetInt(2 * 60000, 5 * 60000));
	}

	private RoundStateHolder CheckAndMergeRoundState(RoundState rs, Dictionary<uint256, RoundStateHolder> requestFromCheckpointList)
	{
		if (!RoundStates.TryGetValue(rs.Id, out RoundStateHolder? rsh))
		{
			return new(rs);
		}

		rsh.VerifyAndSet(rs, requestFromCheckpointList.ContainsKey(rs.Id), _verifyRoundState);
		return rsh;
	}

	private Task<RoundState> CreateRoundAwaiterAsync(uint256? roundId, Phase? phase, Predicate<RoundState>? predicate, CancellationToken cancellationToken)
	{
		RoundStateAwaiter? roundStateAwaiter = null;

		lock (AwaitersLock)
		{
			roundStateAwaiter = new RoundStateAwaiter(predicate, roundId, phase, cancellationToken);
			Awaiters.Add(roundStateAwaiter);
		}

		cancellationToken.Register(() =>
		{
			lock (AwaitersLock)
			{
				Awaiters.Remove(roundStateAwaiter);
			}
		});

		return roundStateAwaiter.Task;
	}

	public Task<RoundState> CreateRoundAwaiterAsync(Predicate<RoundState> predicate, CancellationToken cancellationToken)
	{
		return CreateRoundAwaiterAsync(null, null, predicate, cancellationToken);
	}

	public Task<RoundState> CreateRoundAwaiterAsync(uint256 roundId, Phase phase, CancellationToken cancellationToken)
	{
		return CreateRoundAwaiterAsync(roundId, phase, null, cancellationToken);
	}

	public Task<RoundState> CreateRoundAwaiter(Phase phase, CancellationToken cancellationToken)
	{
		return CreateRoundAwaiterAsync(null, phase, null, cancellationToken);
	}

	/// <summary>
	/// This might not contain up-to-date states. Make sure it is updated.
	/// </summary>
	public bool TryGetRoundState(uint256 roundId, [NotNullWhen(true)] out RoundState? roundState)
	{
		bool res = RoundStates.TryGetValue(roundId, out var roundStateHolder);
		roundState = roundStateHolder?.RoundState;
		return res;
	}

	public override Task StopAsync(CancellationToken cancellationToken)
	{
		lock (AwaitersLock)
		{
			foreach (var awaiter in Awaiters)
			{
				awaiter.Cancel();
			}
		}
		return base.StopAsync(cancellationToken);
	}
}
