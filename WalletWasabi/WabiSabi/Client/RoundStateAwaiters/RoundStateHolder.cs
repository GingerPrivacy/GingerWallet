using Newtonsoft.Json;
using System.Linq;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.WabiSabi.Models.Serialization;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Client.RoundStateAwaiters;

public class RoundStateHolder
{
	public RoundStateHolder(RoundState roundState, string[] allowedCoordinatorIdentifiers, bool verify)
	{
		RoundState = roundState;
		Confidence = 0;
		_roundState = roundState;
		_inputVerifyIndex = 0;
		_inputCount = -1;
		_exception = null;
		_allowedCoordinatorIdentifiers = allowedCoordinatorIdentifiers;
		VerifyAndSet(roundState, false, verify);
	}

	// The round state that the application can see
	public RoundState RoundState { get; private set; }

	public int Confidence { get; private set; }

	private string[] _allowedCoordinatorIdentifiers;

	// This is always the round state that we last get, but we don't give to the others if something is not right
	private RoundState _roundState;

	private int _inputCount;
	private int _inputVerifyIndex;

	private Exception? _exception;

	public Exception? Exception
	{
		get => _exception;
		set
		{
			if (_exception is null && value is not null)
			{
				_exception = value;
				Confidence = -1;
				// Strip away everything, but the RoundCreated event
				RoundCreated? rc = RoundState.CoinjoinState.Events.OfType<RoundCreated>().FirstOrDefault();
				RoundState = RoundState with { CoinjoinState = RoundState.CoinjoinState with { Events = rc is not null ? [rc] : [] } };
				// From this point we don't change the RoundState
			}
		}
	}

	public void VerifyAndSet(RoundState rs, bool checkPoint, bool verify)
	{
		var ors = _roundState;
		var nrs = checkPoint ? rs with { CoinjoinState = rs.CoinjoinState.AddPreviousStates(ors.CoinjoinState) } : rs;

		_roundState = nrs;
		if (Confidence < 0)
		{
			// We are not interested in checking, the RoundState we give is already a stripped one
			return;
		}
		if (!verify)
		{
			RoundState = nrs;
			return;
		}

		if (nrs.InputRegistrationStart != ors.InputRegistrationStart || nrs.InputRegistrationTimeout != ors.InputRegistrationTimeout)
		{
			Exception = new CoinJoinClientException(CoinjoinError.TamperedRoundState, $"Registration time changed at round {ors.Id}.");
			return;
		}

		if (nrs.AmountCredentialIssuerParameters != ors.AmountCredentialIssuerParameters || nrs.VsizeCredentialIssuerParameters != ors.VsizeCredentialIssuerParameters)
		{
			// Something fishy here, tampered with the credentials
			Exception = new CoinJoinClientException(CoinjoinError.TamperedRoundState, $"Credential change at round {ors.Id}.");
			return;
		}

		if (nrs.CoinjoinState.Events.OfType<RoundCreated>().Count() != 1)
		{
			// Possibly protocol error
			Exception = new CoinJoinClientException(CoinjoinError.TamperedRoundState, $"Incorrect RoundCreated event count at round {ors.Id}.");
			return;
		}

		if (!_allowedCoordinatorIdentifiers.Contains(nrs.CoinjoinState.Parameters.CoordinationIdentifier))
		{
			// Not allowed CoordinatorIdentifier
			var list = string.Join(", ", _allowedCoordinatorIdentifiers.Select(x => $"\"{x}\""));
			Exception = new CoinJoinClientException(CoinjoinError.TamperedRoundState, $"Incorrect CoordinatorIdentifier, \"{nrs.CoinjoinState.Parameters.CoordinationIdentifier}\" is not from the list [{list}].");
			return;
		}

		if (nrs.Phase == Phase.OutputRegistration || nrs.Phase == Phase.TransactionSigning)
		{
			var events = nrs.CoinjoinState.Events;
			if (_inputVerifyIndex < events.Count)
			{
				var inputCommitmentData = new CoinJoinInputCommitmentData(ors.CoinjoinState.Parameters.CoordinationIdentifier, ors.Id).ToBytes();
				for (int idx = _inputVerifyIndex; idx < events.Count; idx++)
				{
					if (events[idx] is InputAdded input && !input.OwnershipProof.VerifyOwnership(input.Coin.ScriptPubKey, inputCommitmentData, true))
					{
						Exception = new CoinJoinClientException(CoinjoinError.TamperedRoundState, $"The coordinator is cheating by adding inputs that were created for different rounds at round {ors.Id}.");
						return;
					}
				}
				_inputVerifyIndex = events.Count;

				int inputCount = events.Where(x => x is InputAdded).Count();
				int minInputCount = ors.CoinjoinState.Parameters.MinInputCountByRound;
				if (inputCount < minInputCount)
				{
					Exception = new CoinJoinClientException(CoinjoinError.TamperedRoundState, $"There is only ({inputCount}) inputs instead of the minimum ({minInputCount}), the coordinator still want to continue at round {ors.Id}.");
					return;
				}
				if (_inputCount >= 0 && _inputCount != inputCount)
				{
					Exception = new CoinJoinClientException(CoinjoinError.TamperedRoundState, $"New inputs were added after the output registration at round {ors.Id}.");
					return;
				}
				_inputCount = inputCount;
			}
		}

		if (!checkPoint)
		{
			var olstStr = JsonConvert.SerializeObject(ors.CoinjoinState.Events, JsonSerializationOptions.Default.Settings);
			var nlstStr = JsonConvert.SerializeObject(nrs.CoinjoinState.Events.Take(ors.CoinjoinState.Events.Count), JsonSerializationOptions.Default.Settings);
			if (olstStr != nlstStr)
			{
				Exception = new CoinJoinClientException(CoinjoinError.TamperedRoundState, $"Tampered event elements at round {ors.Id}.");
				return;
			}
			Confidence++;
		}
		// Drop the confidence back when we change a phase
		if (ors.Phase != nrs.Phase)
		{
			Confidence = 1;
		}

		RoundState = nrs;
	}
}
