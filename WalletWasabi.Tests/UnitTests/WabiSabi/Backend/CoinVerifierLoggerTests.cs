using NBitcoin;
using Nito.AsyncEx;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.WabiSabi.Backend.Banning;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class CoinVerifierLoggerTests
{
	[Fact]
	public async Task DisposeWaitsForAnInFlightAuditWriteAsync()
	{
		string directory = TestDirectory.Get();
		await using CoinVerifierLogger logger = new(directory);
		var fileLock = Assert.IsType<AsyncLock>(typeof(CoinVerifierLogger)
			.GetField("<FileAsyncLock>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(logger));
		IDisposable gate = await fileLock.LockAsync();
		Task? save = null;
		Task? disposal = null;
		try
		{
			logger.LogException(uint256.One, "in-flight", new InvalidOperationException("Final audit write."));
			save = logger.SaveAuditsAsync();
			Assert.False(save.IsCompleted);
			disposal = logger.DisposeAsync().AsTask();
			Assert.False(disposal.IsCompleted);
		}
		finally
		{
			gate.Dispose();
			if (save is not null)
			{
				await save.WaitAsync(TimeSpan.FromSeconds(5));
			}
			if (disposal is not null)
			{
				await disposal.WaitAsync(TimeSpan.FromSeconds(5));
			}
		}
		string file = Assert.Single(Directory.GetFiles(directory, "VerifierAudits.*.txt"));
		Assert.Contains("Final audit write.", await File.ReadAllTextAsync(file));
	}

	[Fact]
	public async Task DoesNotAcceptAuditWritesAfterDisposalAsync()
	{
		await using CoinVerifierLogger logger = new(TestDirectory.Get());
		await logger.DisposeAsync();
		Assert.Throws<ObjectDisposedException>(() => logger.LogException(uint256.One, "late", new InvalidOperationException("Late audit write.")));
	}
}
