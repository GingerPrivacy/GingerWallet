using Nito.AsyncEx;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.WabiSabi.Backend.Banning;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class WhitelistTests
{
	[Fact]
	public async Task FinalSaveWaitsForAnInFlightWriteAsync()
	{
		string path = Path.Combine(TestDirectory.Get(), "Whitelist.txt");
		Whitelist whitelist = new([], path, WabiSabiTestFactory.CreateDefaultWabiSabiConfig());
		var outpoint = BitcoinFactory.CreateOutPoint(TestRandom.Get());
		whitelist.Add(outpoint);
		var fileLock = Assert.IsType<AsyncLock>(typeof(Whitelist)
			.GetField("<FileLock>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(whitelist));
		IDisposable gate = await fileLock.LockAsync();
		Task<bool>? firstSave = null;
		Task<bool>? finalSave = null;
		try
		{
			firstSave = whitelist.WriteToFileIfChangedAsync();
			Assert.False(firstSave.IsCompleted);
			finalSave = whitelist.WriteToFileIfChangedAsync();
			Assert.False(finalSave.IsCompleted);
		}
		finally
		{
			gate.Dispose();
			if (firstSave is not null)
			{
				await firstSave.WaitAsync(TimeSpan.FromSeconds(5));
			}
			if (finalSave is not null)
			{
				await finalSave.WaitAsync(TimeSpan.FromSeconds(5));
			}
		}
		Assert.Contains(await File.ReadAllLinesAsync(path), line => Innocent.TryReadFromString(line, out var innocent) && innocent.Outpoint == outpoint);
	}

	[Fact]
	public void WhitelistChangeTrafficAsync()
	{
		var rnd = TestRandom.Get();
		var cfg = WabiSabiTestFactory.CreateDefaultWabiSabiConfig();
		cfg.ReleaseFromWhitelistAfter = TimeSpan.Zero;

		Whitelist whitelist = new(Enumerable.Empty<Innocent>(), string.Empty, cfg);
		var currentChangeId = whitelist.ChangeId;

		var outpoint = BitcoinFactory.CreateOutPoint(rnd);
		whitelist.Add(outpoint);
		Assert.NotEqual(currentChangeId, whitelist.ChangeId);
		currentChangeId = whitelist.ChangeId;

		var outpoint2 = BitcoinFactory.CreateOutPoint(rnd);
		whitelist.Add(outpoint2);
		Assert.NotEqual(currentChangeId, whitelist.ChangeId);
		currentChangeId = whitelist.ChangeId;

		Assert.True(whitelist.TryRelease(outpoint));
		Assert.NotEqual(currentChangeId, whitelist.ChangeId);

		var outpoint3 = BitcoinFactory.CreateOutPoint(rnd);
		whitelist.Add(outpoint3);
		Assert.NotEqual(currentChangeId, whitelist.ChangeId);

		whitelist.RemoveAllExpired();

		Assert.Equal(0, whitelist.CountInnocents());
	}
}
