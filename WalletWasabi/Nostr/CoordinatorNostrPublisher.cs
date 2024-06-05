using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Secp256k1;
using NNostr.Client;
using WalletWasabi.Bases;

namespace WalletWasabi.Nostr;

public class CoordinatorNostrPublisher : PeriodicRunner
{
	private readonly Uri[] _relayUris = [new("wss://relay.primal.net")];

	public CoordinatorNostrPublisher(TimeSpan period, ECPrivKey key, NostrCoordinator coordinator) : base(period)
	{
		Coordinator = coordinator;
		Key = key;
		Client = NostrExtensions.Create(_relayUris, (EndPoint?)null);
	}

	private INostrClient Client { get; }

	private NostrCoordinator Coordinator { get; }

	private ECPrivKey Key { get; }

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var discoveryEvent = await Key.CreateCoordinatorDiscoveryEventAsync(Coordinator).ConfigureAwait(false);

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancel);
		await Client.PublishAsync([discoveryEvent], linkedCts.Token).ConfigureAwait(false);
	}

	public override void Dispose()
	{
		Key.Dispose();
		base.Dispose();
	}
}
