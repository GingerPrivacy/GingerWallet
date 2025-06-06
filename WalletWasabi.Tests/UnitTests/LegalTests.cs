using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Legal;
using WalletWasabi.Services;
using WalletWasabi.Tests.TestCommon;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class LegalTests
{
	[Fact]
	public async Task LeavesTrashAloneAsync()
	{
		var legalDir = TestDirectory.Get();

		var res = await LegalDocuments.LoadAgreedAsync(legalDir);
		Assert.Null(res);

		// Leaves one trash alone.
		var trash1 = File.Create(Path.Combine(legalDir, "foo"));
		await trash1.DisposeAsync();
		res = await LegalDocuments.LoadAgreedAsync(legalDir);
		Assert.Null(res);
		// 1 legal file + lock.txt from TestDirectory.Get()
		Assert.Equal(2, Directory.GetFiles(legalDir).Length);
		Assert.Empty(Directory.GetDirectories(legalDir));

		// Leaves 3 trash alone.
		var trash2 = File.Create(Path.Combine(legalDir, "foo.txt"));
		var trash3 = File.Create(Path.Combine(legalDir, "foo2"));
		await trash2.DisposeAsync();
		await trash3.DisposeAsync();
		res = await LegalDocuments.LoadAgreedAsync(legalDir);
		Assert.Null(res);
		// 3 legal files + lock.txt from TestDirectory.Get()
		Assert.Equal(4, Directory.GetFiles(legalDir).Length);
		Assert.Empty(Directory.GetDirectories(legalDir));
	}

	[Fact]
	public async Task ResolvesConflictsAsync()
	{
		var legalDir = TestDirectory.Get();

		var res = await LegalDocuments.LoadAgreedAsync(legalDir);
		Assert.Null(res);

		// Deletes them if multiple legal docs found.
		var candidate1 = File.Create(Path.Combine(legalDir, "1.1.txt"));
		var candidate2 = File.Create(Path.Combine(legalDir, "1.2.txt"));
		await candidate1.DisposeAsync();
		await candidate2.DisposeAsync();
		res = await LegalDocuments.LoadAgreedAsync(legalDir);
		Assert.Null(res);
		// only lock.txt from TestDirectory.Get()
		Assert.Single(Directory.GetFiles(legalDir));
		Assert.Empty(Directory.GetDirectories(legalDir));

		// Only the candidates are deleted.
		var trash = File.Create(Path.Combine(legalDir, "1.txt"));
		candidate1 = File.Create(Path.Combine(legalDir, "1.1.txt"));
		candidate2 = File.Create(Path.Combine(legalDir, "1.2.txt"));
		await trash.DisposeAsync();
		await candidate1.DisposeAsync();
		await candidate2.DisposeAsync();
		res = await LegalDocuments.LoadAgreedAsync(legalDir);
		Assert.Null(res);
		Assert.Equal(2, Directory.GetFiles(legalDir).Length);
		Assert.Empty(Directory.GetDirectories(legalDir));
	}

	[Fact]
	public async Task CanLoadLegalDocsAsync()
	{
		var legalDir = TestDirectory.Get();

		var res = await LegalDocuments.LoadAgreedAsync(legalDir);
		Assert.Null(res);

		var version = new Version(1, 1);

		// Deletes them if multiple legal docs found.
		var candidate = File.Create(Path.Combine(legalDir, $"{version}.txt"));
		await candidate.DisposeAsync();
		res = await LegalDocuments.LoadAgreedAsync(legalDir);
		Assert.NotNull(res);
		// the legal docs + the lock.txt from TestDirectory.Get()
		Assert.Equal(2, Directory.GetFiles(legalDir).Length);
		Assert.Empty(Directory.GetDirectories(legalDir));
		Assert.Equal(version, res?.Version);
	}

	[Fact]
	public async Task CanSerializeFileAsync()
	{
		Version version = new(1, 1);
		LegalDocuments legal = new(version, "Don't trust, verify!");
		string legalFolderPath = Path.Combine(TestDirectory.Get(), LegalChecker.LegalFolderName);
		await legal.ToFileAsync(legalFolderPath);

		string expectedFilePath = $"{Path.Combine(legalFolderPath, version.ToString())}.txt";
		Assert.True(File.Exists(expectedFilePath));
	}
}
