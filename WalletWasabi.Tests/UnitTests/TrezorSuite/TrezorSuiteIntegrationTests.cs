using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using WalletWasabi.Daemon;
using WalletWasabi.TrezorSuite;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.TrezorSuite;

public class TrezorSuiteIntegrationTests
{
	[Fact]
	public void PrivacyTargetDefaultsToThreeAndOldBackupsRemainReadable()
	{
		Assert.Equal(3, new UiConfig().TrezorSuiteTargetAnonymity);
		var state = JsonSerializer.Deserialize<TrezorSuiteIntegrationState>("""
			{"SuiteExecutablePath":"suite","OriginalSettingsExisted":false,"OriginalSettingsJson":null,"LastVerifiedAt":null}
			""");
		Assert.NotNull(state);
		Assert.Null(state.OriginalAnonymityTargets);
		Assert.Null(state.AppliedAnonymityTarget);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(101)]
	public async Task InvalidPrivacyTargetIsRejectedBeforeLaunchingSuite(int target)
	{
		using var service = new TrezorSuiteIntegrationService(Path.GetTempPath());
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ConfigureAndLaunchAsync(targetAnonymity: target));
	}

	[Theory]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(100)]
	public void PrivacyScriptUsesValidatedTarget(int target)
	{
		Assert.Contains($"const target = {target};", TrezorSuitePrivacyScripts.Apply(target, new()));
	}

	[Fact]
	public void LocatorPrefersExplicitExecutablePath()
	{
		var temporaryDirectory = Directory.CreateTempSubdirectory("ginger-trezor-locator-");
		try
		{
			var preferredPath = Path.Combine(temporaryDirectory.FullName, "preferred-suite");
			var previousPath = Path.Combine(temporaryDirectory.FullName, "previous-suite");
			File.WriteAllText(preferredPath, "test");
			File.WriteAllText(previousPath, "test");

			var result = new TrezorSuiteLocator().Locate(preferredPath, previousPath);

			Assert.Equal(Path.GetFullPath(preferredPath), result, TrezorSuiteLocator.PathComparer);
		}
		finally
		{
			temporaryDirectory.Delete(recursive: true);
		}
	}

	[Fact]
	public void ApplyScriptContainsCompleteGingerOverride()
	{
		var script = TrezorSuiteCdpClient.BuildApplyOverrideScript();

		Assert.Contains("https://api.gingerwallet.io/WabiSabi/", script, StringComparison.Ordinal);
		Assert.Contains("https://api.gingerwallet.io/", script, StringComparison.Ordinal);
		Assert.Contains("affiliationId: null", script, StringComparison.Ordinal);
		Assert.Contains("coinjoinDebugSettings", script, StringComparison.Ordinal);
		Assert.Contains("put(value, 'debug')", script, StringComparison.Ordinal);
		Assert.DoesNotContain("try {\n\t\t\t\t\t{", script, StringComparison.Ordinal);
	}

	[Fact]
	public void RestoreScriptPreservesSerializedOriginalValue()
	{
		const string OriginalJson = "{\"coinjoinServerEnvironment\":{\"btc\":\"staging\"},\"note\":\"'quoted'\"}";

		var script = TrezorSuiteCdpClient.BuildRestoreSettingsScript(true, OriginalJson);

		Assert.Contains("const originallyExisted = true", script, StringComparison.Ordinal);
		Assert.Contains("JSON.parse(\"{\\u0022coinjoinServerEnvironment", script, StringComparison.Ordinal);
		Assert.Contains("writeDebugSettings(db, original)", script, StringComparison.Ordinal);
	}

	[Fact]
	public async Task StatusReportsDetectedSuiteAndAvailableBackendAsync()
	{
		var temporaryDirectory = Directory.CreateTempSubdirectory("ginger-trezor-status-");
		try
		{
			var executablePath = Path.Combine(temporaryDirectory.FullName, "trezor-suite");
			File.WriteAllText(executablePath, "test");
			using var handler = new SuccessfulBackendHandler();
#pragma warning disable RS0030 // A controlled test handler requires constructing its client directly.
			using var httpClient = new HttpClient(handler, disposeHandler: false);
#pragma warning restore RS0030
			using var service = new TrezorSuiteIntegrationService(temporaryDirectory.FullName, httpClient);

			var status = await service.GetStatusAsync(executablePath);

			Assert.True(status.IsInstalled);
			Assert.True(status.IsBackendAvailable);
			Assert.False(status.IsConfigured);
			Assert.False(status.HasBackup);
			Assert.Equal(Path.GetFullPath(executablePath), status.ExecutablePath, TrezorSuiteLocator.PathComparer);
		}
		finally
		{
			temporaryDirectory.Delete(recursive: true);
		}
	}

	[Fact]
	public void SuiteLaunchArgumentsEnableDiagnosticFileLogging()
	{
		var temporaryDirectory = Directory.CreateTempSubdirectory("ginger-trezor-logs-");
		try
		{
			using var service = new TrezorSuiteIntegrationService(temporaryDirectory.FullName);

			var arguments = service.GetLaunchArguments();

			Assert.Contains("--log-write", arguments);
			Assert.Contains("--log-level=debug", arguments);
			Assert.Contains("--log-file=trezor-suite-log-%ts.txt", arguments);
			Assert.Contains($"--log-path={service.LogDirectoryPath}", arguments);
			Assert.Equal(
				Path.Combine(temporaryDirectory.FullName, "TrezorSuiteIntegration", "SuiteLogs"),
				service.LogDirectoryPath);
		}
		finally
		{
			temporaryDirectory.Delete(recursive: true);
		}
	}

	[Fact]
	public void BootstrapArgumentsIncludeDiagnosticFileLogging()
	{
		using var service = new TrezorSuiteIntegrationService(Path.GetTempPath());

		var arguments = service.GetBootstrapArguments(12345);

		Assert.Contains("--log-write", arguments);
		Assert.Contains("--log-level=debug", arguments);
		Assert.Contains("--remote-debugging-address=127.0.0.1", arguments);
		Assert.Contains("--remote-debugging-port=12345", arguments);
	}

	private sealed class SuccessfulBackendHandler : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				RequestMessage = request,
				Content = new StringContent("{}")
			});
		}
	}
}
