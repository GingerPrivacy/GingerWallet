using System.Globalization;
using WalletWasabi.Lang;

namespace WalletWasabi.BitcoinCore.Monitoring;

public class RpcStatus : IEquatable<RpcStatus>
{
	private RpcStatus(bool success, ulong headers, ulong blocks, int peersCount)
	{
		Synchronized = false;
		if (success)
		{
			var diff = headers - blocks;
			if (peersCount == 0)
			{
				Status = Resources.FullNodeConnecting;
			}
			else if (diff == 0)
			{
				Synchronized = true;
				Status = Resources.FullNodeSynchronized;
			}
			else
			{
				Status = string.Format(CultureInfo.InvariantCulture, Resources.FullNodeDownloadingBlocks, diff);
			}
		}
		else
		{
			Status = Resources.FullNodeUnresponsive;
		}

		Success = success;
		Headers = headers;
		Blocks = blocks;
		PeersCount = peersCount;
	}

	private RpcStatus(string status)
	{
		Status = status;
		Success = false;
		Headers = 0;
		Blocks = 0;
		PeersCount = 0;
		Synchronized = false;
	}

	public static RpcStatus Unresponsive { get; } = new RpcStatus(false, 0, 0, 0);
	public static RpcStatus Connecting { get; } = new RpcStatus(Resources.FullNodeConnecting);

	public string Status { get; }
	public bool Success { get; }
	public ulong Headers { get; }
	public ulong Blocks { get; }
	public int PeersCount { get; }
	public bool Synchronized { get; }

	public static RpcStatus Responsive(ulong headers, ulong blocks, int peersCount) => new(true, headers, blocks, peersCount);

	public override string ToString() => Status;

	#region EqualityAndComparison

	public override bool Equals(object? obj) => Equals(obj as RpcStatus);

	public bool Equals(RpcStatus? other) => this == other;

	public override int GetHashCode() => HashCode.Combine(Status, Success, Headers, Blocks, PeersCount, Synchronized);

	public static bool operator ==(RpcStatus? x, RpcStatus? y) =>
		x is null ? y is null :
		y is not null &&
		x.Status == y.Status &&
		x.Success == y.Success &&
		x.Headers == y.Headers &&
		x.Blocks == y.Blocks &&
		x.PeersCount == y.PeersCount &&
		x.Synchronized == y.Synchronized;

	public static bool operator !=(RpcStatus? x, RpcStatus? y) => !(x == y);

	#endregion EqualityAndComparison
}
