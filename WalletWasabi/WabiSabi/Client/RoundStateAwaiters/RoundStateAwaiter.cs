using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.RoundStateAwaiters;

public record RoundStateAwaiter
{
	public RoundStateAwaiter(
		Predicate<RoundState>? predicate,
		uint256? roundId,
		Phase? phase,
		CancellationToken cancellationToken)
	{
		if (predicate is null && phase is null)
		{
			throw new ArgumentNullException(nameof(predicate));
		}

		TaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		Predicate = predicate;
		RoundId = roundId;
		Phase = phase;
		cancellationToken.Register(() => Cancel());
	}

	private TaskCompletionSource<RoundState> TaskCompletionSource { get; }
	private Predicate<RoundState>? Predicate { get; }
	private uint256? RoundId { get; }
	private Phase? Phase { get; }

	public Task<RoundState> Task => TaskCompletionSource.Task;

	public bool IsCompleted(IDictionary<uint256, RoundStateHolder> allRoundStates)
	{
		if (Task.IsCompleted)
		{
			return true;
		}

		if (RoundId is not null)
		{
			if (!allRoundStates.TryGetValue(RoundId, out RoundStateHolder? roundStateHolder))
			{
				TaskCompletionSource.TrySetException(new InvalidOperationException($"Round {RoundId} is not running anymore."));
				return true;
			}
			return CheckAndSetRoundStateHolder(roundStateHolder);
		}

		return allRoundStates.Values.FirstOrDefault(CheckAndSetRoundStateHolder) is not null;
	}

	protected bool CheckAndSetRoundStateHolder(RoundStateHolder roundStateHolder)
	{
		if (Phase is { } expectedPhase)
		{
			if (roundStateHolder.RoundState.Phase > expectedPhase)
			{
				TaskCompletionSource.TrySetException(new UnexpectedRoundPhaseException(RoundId ?? uint256.Zero, expectedPhase, roundStateHolder.RoundState));
				return true;
			}

			if (roundStateHolder.RoundState.Phase != expectedPhase)
			{
				return false;
			}
		}

		if (Predicate is { } && !Predicate(roundStateHolder.RoundState))
		{
			return false;
		}

		if (roundStateHolder.Exception is not null)
		{
			TaskCompletionSource.TrySetException(roundStateHolder.Exception);
		}
		else
		{
			TaskCompletionSource.SetResult(roundStateHolder.RoundState);
		}
		return true;
	}

	public void Cancel()
	{
		TaskCompletionSource.TrySetCanceled();
	}
}
