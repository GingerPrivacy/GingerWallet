using System.IO;
using System.Text;
using System.Text.Json;
using WalletWasabi.Bases;
using WalletWasabi.Interfaces;
using WalletWasabi.Tests.TestCommon;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.StandaloneTests;

public class ConfigManagerNgTests
{
	[Theory]
	[InlineData("{\"Value\":")]
	[InlineData("null")]
	[InlineData("{\"Value\":\"invalid\"}")]
	public void InvalidConfigurationIsPreserved(string json)
	{
		string path = Path.Combine(TestDirectory.Get(), $"{Guid.NewGuid()}.json");
		byte[] original = Encoding.UTF8.GetBytes(json);
		File.WriteAllBytes(path, original);

		var exception = Assert.Throws<InvalidDataException>(() => ConfigManagerNg.LoadFile<TestConfig>(path, createIfMissing: true));

		Assert.IsType<JsonException>(exception.InnerException);
		Assert.Equal(original, File.ReadAllBytes(path));
		Assert.Equal(42, ConfigManagerNg.LoadFile<TestConfig>(Path.ChangeExtension(path, "Default.json")).Value);
	}

	[Fact]
	public void ExistingRecoveryTemplateIsPreserved()
	{
		string path = Path.Combine(TestDirectory.Get(), "Config.json");
		string defaultPath = Path.ChangeExtension(path, "Default.json");
		File.WriteAllText(path, "invalid");
		File.WriteAllText(defaultPath, "user-edited template");

		Assert.Throws<InvalidDataException>(() => ConfigManagerNg.LoadFile<TestConfig>(path, createIfMissing: true));

		Assert.Equal("invalid", File.ReadAllText(path));
		Assert.Equal("user-edited template", File.ReadAllText(defaultPath));
	}

	[Fact]
	public void TemplateWriteFailureStillPreservesConfiguration()
	{
		string path = Path.Combine(TestDirectory.Get(), "Config.json");
		File.WriteAllText(path, "invalid");
		Directory.CreateDirectory(Path.ChangeExtension(path, "Default.json"));

		var exception = Assert.Throws<InvalidDataException>(() => ConfigManagerNg.LoadFile<TestConfig>(path, createIfMissing: true));

		Assert.IsType<JsonException>(exception.InnerException);
		Assert.Equal("invalid", File.ReadAllText(path));
	}

	[Fact]
	public void MissingConfigurationIsCreated()
	{
		string path = Path.Combine(TestDirectory.Get(), "Config.json");

		Assert.Equal(42, ConfigManagerNg.LoadFile<TestConfig>(path, createIfMissing: true).Value);
		Assert.Equal(42, ConfigManagerNg.LoadFile<TestConfig>(path).Value);
		Assert.False(File.Exists(Path.ChangeExtension(path, "Default.json")));
	}

	[Fact]
	public void ValidConfigurationIsLoadedWithoutRewriting()
	{
		string path = Path.Combine(TestDirectory.Get(), "Config.json");
		const string json = "{ \"Value\": 7 }";
		File.WriteAllText(path, json);

		Assert.Equal(7, ConfigManagerNg.LoadFile<TestConfig>(path, createIfMissing: true).Value);
		Assert.Equal(json, File.ReadAllText(path));
		Assert.False(File.Exists(Path.ChangeExtension(path, "Default.json")));
	}

	public class TestConfig : IConfigNg
	{
		public int Value { get; set; } = 42;
	}
}
