using NBitcoin;
using NBitcoin.Protocol;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.Wallets;

public class P2PNodesManager
{
	public P2PNodesManager(Network network, NodesGroup nodes, bool isTorEnabled)
	{
		Network = network;
		Nodes = nodes;
		IsTorEnabled = isTorEnabled;
	}

	private Network Network { get; }
	private NodesGroup Nodes { get; }
	private bool IsTorEnabled { get; }
	private int NodeTimeouts { get; set; }
	public uint ConnectedNodesCount => (uint)Nodes.ConnectedNodes.Count;

	private readonly HashSet<Node> _nodesInUse = new();

	public async Task<Node> GetNodeAsync(CancellationToken cancellationToken)
	{
		do
		{
			if (Nodes.ConnectedNodes.Count > 0)
			{
				var node = Nodes.ConnectedNodes.Where(n => !_nodesInUse.Contains(n)).RandomElement(SecureRandom.Instance);

				if (node is not null && node.IsConnected)
				{
					_nodesInUse.Add(node);
					return node;
				}
			}

			await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);

			cancellationToken.ThrowIfCancellationRequested();
		}
		while (true);
	}

	public void DisconnectNodeIfEnoughPeers(Node node, string reason)
	{
		if (Nodes.ConnectedNodes.Count > 5)
		{
			DisconnectNode(node, reason);
		}
	}

	public void DisconnectNode(Node node, string reason)
	{
		Logger.LogInfo(reason);
		node.DisconnectAsync(reason);
	}

	public bool TryReleaseNode(Node? node)
	{
		if (node is null)
		{
			return false;
		}

		return _nodesInUse.Remove(node);
	}

	public double GetCurrentTimeout()
	{
		// More permissive timeout if few nodes are connected to avoid exhaustion.
		return Nodes.ConnectedNodes.Count < 3
			? Math.Min(RuntimeParams.Instance.NetworkNodeTimeout * 1.5, 600)
			: RuntimeParams.Instance.NetworkNodeTimeout;
	}

	/// <summary>
	/// Current timeout used when downloading a block from the remote node. It is defined in seconds.
	/// </summary>
	public async Task UpdateTimeoutAsync(bool increaseDecrease)
	{
		if (increaseDecrease)
		{
			NodeTimeouts++;
		}
		else
		{
			NodeTimeouts--;
		}

		var timeout = RuntimeParams.Instance.NetworkNodeTimeout;

		// If it times out 2 times in a row then increase the timeout.
		if (NodeTimeouts >= 2)
		{
			NodeTimeouts = 0;
			timeout = (int)Math.Round(timeout * 1.5);
		}
		else if (NodeTimeouts <= -3) // If it does not time out 3 times in a row, lower the timeout.
		{
			NodeTimeouts = 0;
			timeout = (int)Math.Round(timeout * 0.7);
		}

		// Sanity check
		var minTimeout = Network == Network.Main ? 3 : 2;
		minTimeout = IsTorEnabled ? (int)Math.Round(minTimeout * 1.5) : minTimeout;

		if (timeout < minTimeout)
		{
			timeout = minTimeout;
		}
		else if (timeout > 600)
		{
			timeout = 600;
		}

		if (timeout == RuntimeParams.Instance.NetworkNodeTimeout)
		{
			return;
		}

		RuntimeParams.Instance.NetworkNodeTimeout = timeout;
		await RuntimeParams.Instance.SaveAsync().ConfigureAwait(false);

		Logger.LogInfo($"Current timeout value used on block download is: {timeout} seconds.");
	}
}
