using Nito.AsyncEx;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinCore.Monitoring;

public class P2pReconnector : PeriodicRunner
{
	public P2pReconnector(TimeSpan period, P2pNode p2pNode) : base(period)
	{
		P2pNode = Guard.NotNull(nameof(p2pNode), p2pNode);
	}

	public P2pNode P2pNode { get; }
	private TaskCompletionSource<bool> Success { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private AsyncLock ReconnectionLock { get; } = new();

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		Logger.LogInfo("Trying to reconnect to P2P...");
		if (await P2pNode.TryDisconnectAsync(cancel).ConfigureAwait(false))
		{
			await P2pNode.ConnectAsync(cancel).ConfigureAwait(false);

			Logger.LogInfo("Successfully reconnected to P2P.");
			Success.TrySetResult(true);
		}
	}

	public async Task StartAndAwaitReconnectionAsync(CancellationToken cancel)
	{
		using (await ReconnectionLock.LockAsync(cancel).ConfigureAwait(false))
		{
			Success = new(TaskCreationOptions.RunContinuationsAsynchronously);
			try
			{
				await StartAsync(cancel).ConfigureAwait(false);
				await Success.Task.WaitAsync(cancel).ConfigureAwait(false);
			}
			finally
			{
				try
				{
					using var cts = new CancellationTokenSource(Period * 2);
					await StopAsync(cts.Token).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
				}
			}
		}
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		Task stop = base.StopAsync(cancellationToken);
		Success.TrySetCanceled(CancellationToken.None);
		await stop.ConfigureAwait(false);
	}
}
