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

	public CoordinatorNostrPublisher(TimeSpan period, Uri coordinatorUri, Network network) : base(period)
	{
		Client = NostrExtensions.Create(_relayUris, (EndPoint?)null);
		Coordinator = new("Test", "Test Coordinator", coordinatorUri, network);
	}

	private INostrClient Client { get; }

	private NostrCoordinator Coordinator { get; }

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		// 1. Generate a new private key
		var key = new Key();
		if (!Context.Instance.TryCreateECPrivKey(key.ToBytes(), out var ecPrivKey))
		{
			throw new InvalidOperationException("Failed to create ECPrivKey");
		}

		// 2. Generate the discovery event
		var discoveryEvent = await ecPrivKey.CreateCoordinatorDiscoveryEventAsync(Coordinator).ConfigureAwait(false);

		// 3. Send out the event
		var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancel);
		await Client.PublishAsync([discoveryEvent], linkedCts.Token).ConfigureAwait(false);

		// 4. Clean up
		key.Dispose();
		ecPrivKey.Dispose();
		cts.Dispose();
		linkedCts.Dispose();
	}
}
