using NBitcoin;
using System.Collections.Generic;
using WabiSabi.Crypto.Randomness;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public class BlameRound : Round
{
	public BlameRound(RoundParameters parameters, Round blameOf, ISet<OutPoint> blameWhitelist, WasabiRandom random)
		: base(parameters, random, parameters.BlameInputRegistrationTimeout)
	{
		BlameOf = blameOf;
		BlameWhitelist = blameWhitelist;
	}

	public Round BlameOf { get; }
	public ISet<OutPoint> BlameWhitelist { get; }

	public override bool IsInputRegistrationEnded(int maxInputCount)
	{
		return base.IsInputRegistrationEnded(BlameWhitelist.Count);
	}
}
