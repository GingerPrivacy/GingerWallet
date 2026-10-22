using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifyItem : IDisposable
{
	private bool _disposedValue;

	public CoinVerifyItem()
	{
		Token = AbortCts.Token;
	}

	public DateTimeOffset ScheduleTime { get; } = DateTimeOffset.UtcNow;
	private TaskCompletionSource<CoinVerifyResult> TaskCompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private CancellationTokenSource AbortCts { get; } = new();
	private object StateLock { get; } = new();
	public Task<CoinVerifyResult> Task => TaskCompletionSource.Task;
	public bool IsCancellationRequested => Token.IsCancellationRequested;
	public CancellationToken Token { get; }

	public void Cancel()
	{
		lock (StateLock)
		{
			if (!_disposedValue)
			{
				AbortCts.Cancel();
			}
		}
	}

	protected virtual void Dispose(bool disposing)
	{
		lock (StateLock)
		{
			if (!_disposedValue)
			{
				_disposedValue = true;
				if (disposing)
				{
					AbortCts.Cancel();
					AbortCts.Dispose();
				}
			}
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	public void SetResult(CoinVerifyResult coinVerifyResult)
	{
		TaskCompletionSource.SetResult(coinVerifyResult);
	}

	public void ThrowIfCancellationRequested()
	{
		Token.ThrowIfCancellationRequested();
	}
}
