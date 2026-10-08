using NBitcoin;
using NBitcoin.Protocol;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class PeerFeeFilterTests
{
	[Theory]
	[InlineData(NodeState.HandShaked, true, 0.1, 0.1, true)]
	[InlineData(NodeState.HandShaked, true, 0.1, 0.099, false)]
	[InlineData(NodeState.HandShaked, true, 1.0, 0.1, false)]
	[InlineData(NodeState.HandShaked, true, null, 0.1, false)]
	[InlineData(NodeState.HandShaked, true, null, 1.0, true)]
	[InlineData(NodeState.HandShaked, true, 0.1, null, true)]
	[InlineData(NodeState.HandShaked, false, 0.1, 0.1, false)]
	[InlineData(NodeState.Connected, true, 0.1, 0.1, false)]
	[InlineData(NodeState.Offline, true, 0.1, 0.1, false)]
	public void RelayCandidatesRespectHandshakePreferenceAndFeeFloor(NodeState state, bool relay, double? floor, double? rate, bool expected)
	{
		Assert.Equal(expected, TransactionBroadcaster.CanRelayTransaction(state, relay,
			floor is { } value ? new FeeRate((decimal)value) : null, rate is { } requested ? new FeeRate((decimal)requested) : null));
	}
}
