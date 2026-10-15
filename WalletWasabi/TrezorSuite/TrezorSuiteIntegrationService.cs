using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.TrezorSuite;

public sealed class TrezorSuiteIntegrationService : IDisposable
{
	private static readonly Uri GingerBackendUri = new("https://api.gingerwallet.io/");
	private static readonly TimeSpan SuiteStartupTimeout = TimeSpan.FromSeconds(25);
	private readonly HttpClient _httpClient;
	private readonly bool _ownsHttpClient;
	private readonly TrezorSuiteLocator _locator;
	private readonly SemaphoreSlim _operationLock = new(1, 1);
	private readonly string _stateFilePath;

	public TrezorSuiteIntegrationService(string gingerDataDir, HttpClient? httpClient = null, TrezorSuiteLocator? locator = null)
	{
		var integrationDataDir = Path.Combine(gingerDataDir, "TrezorSuiteIntegration");
		_stateFilePath = Path.Combine(integrationDataDir, "state.json");
		LogDirectoryPath = Path.Combine(integrationDataDir, "SuiteLogs");
		_httpClient = httpClient ?? WasabiHttpClientFactory.CreateLongLivedHttpClient();
		_ownsHttpClient = httpClient is null;
		_locator = locator ?? new TrezorSuiteLocator();
	}

	public string LogDirectoryPath { get; }

	public async Task<TrezorSuiteIntegrationStatus> GetStatusAsync(string? preferredPath = null, CancellationToken cancellationToken = default)
	{
		var state = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
		var executablePath = _locator.Locate(preferredPath, state?.SuiteExecutablePath);
		var backendAvailable = await IsBackendAvailableAsync(cancellationToken).ConfigureAwait(false);
		var isConfigured = executablePath is not null &&
			state?.LastVerifiedAt is not null &&
			TrezorSuiteLocator.PathComparer.Equals(executablePath, state.SuiteExecutablePath);

		var message = executablePath is null
			? "Trezor Suite was not found. Select its executable to continue."
			: !backendAvailable
				? "Ginger's CoinJoin backend is currently unavailable."
				: isConfigured
					? state!.AppliedAnonymityTarget is { } target
						? $"Ginger CoinJoin was configured. Privacy target {target} was verified for {state.PrivacyAccountCount} saved Bitcoin CoinJoin account(s)." +
							(state.PrivacyAccountCount == 0 ? " No saved accounts: create and remember an account in Suite, select Custom, then configure again to apply the target." : "")
						: "Ginger CoinJoin was configured and verified."
					: "Trezor Suite is ready to be configured for Ginger CoinJoin.";

		return new TrezorSuiteIntegrationStatus(
			executablePath,
			TryGetSuiteVersion(executablePath),
			executablePath is not null,
			backendAvailable,
			isConfigured,
			state is not null,
			isConfigured ? state?.LastVerifiedAt : null,
			message);
	}

	public async Task<TrezorSuiteIntegrationStatus> ConfigureAndLaunchAsync(string? preferredPath = null, CancellationToken cancellationToken = default, int targetAnonymity = 3)
	{
		TrezorSuitePrivacyScripts.ValidateTarget(targetAnonymity);
		await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var state = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
			var executablePath = RequireExecutablePath(preferredPath, state?.SuiteExecutablePath);

			if (!await IsBackendAvailableAsync(cancellationToken).ConfigureAwait(false))
			{
				throw new TrezorSuiteIntegrationException("Ginger's CoinJoin backend is unavailable. Check your connection and try again.");
			}

			Process? bootstrapProcess = null;
			var bootstrapStarted = false;
			try
			{
				var port = GetUnusedLoopbackPort();
				bootstrapProcess = StartSuite(executablePath, GetBootstrapArguments(port));
				bootstrapStarted = true;

				await using var cdpClient = await TrezorSuiteCdpClient.ConnectAsync(
					port,
					SuiteStartupTimeout,
					cancellationToken).ConfigureAwait(false);

				if (state is null)
				{
					var originalSettings = await cdpClient.ReadDebugSettingsAsync(cancellationToken).ConfigureAwait(false);
					state = new TrezorSuiteIntegrationState(
						executablePath,
						originalSettings.Exists,
						originalSettings.Json,
						LastVerifiedAt: null);
					await SaveStateAsync(state, cancellationToken).ConfigureAwait(false);
				}

				// Back up targets separately from debug settings, including accounts added since the first configuration.
				state = state with { LastVerifiedAt = null };
				await SaveStateAsync(state, cancellationToken).ConfigureAwait(false);
				var privacy = await cdpClient.EvaluateAsync(TrezorSuitePrivacyScripts.Read(), cancellationToken).ConfigureAwait(false);
				var targets = privacy.GetProperty("targets").Deserialize<Dictionary<string, int>>()!;
				var originals = new Dictionary<string, int>(state.OriginalAnonymityTargets ?? new());
				foreach (var (key, value) in targets)
				{
					originals.TryAdd(key, value);
				}
				state = state with { OriginalAnonymityTargets = originals, LastVerifiedAt = null };
				await SaveStateAsync(state, cancellationToken).ConfigureAwait(false);
				await cdpClient.ApplyGingerOverrideAsync(cancellationToken).ConfigureAwait(false);
				var applied = await cdpClient.EvaluateAsync(TrezorSuitePrivacyScripts.Apply(targetAnonymity, originals), cancellationToken).ConfigureAwait(false);
				state = state with
				{
					SuiteExecutablePath = executablePath,
					AppliedAnonymityTarget = targetAnonymity,
					PrivacyAccountCount = applied.GetProperty("count").GetInt32(),
					LastVerifiedAt = DateTimeOffset.UtcNow
				};
				await SaveStateAsync(state, cancellationToken).ConfigureAwait(false);
			}
			catch
			{
				if (bootstrapStarted)
				{
					await StopBootstrapProcessAsync(bootstrapProcess).ConfigureAwait(false);
				}

				throw;
			}

			await StopBootstrapProcessAsync(bootstrapProcess).ConfigureAwait(false);
			StartSuite(executablePath, GetLaunchArguments()).Dispose();
			return await GetStatusAsync(executablePath, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_operationLock.Release();
		}
	}

	public async Task<TrezorSuiteIntegrationStatus> RestoreAndLaunchAsync(string? preferredPath = null, CancellationToken cancellationToken = default)
	{
		await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var state = await LoadStateAsync(cancellationToken).ConfigureAwait(false)
				?? throw new TrezorSuiteIntegrationException("No original Trezor Suite configuration backup is available.");
			var executablePath = RequireExecutablePath(preferredPath, state.SuiteExecutablePath);
			Process? bootstrapProcess = null;

			try
			{
				var port = GetUnusedLoopbackPort();
				bootstrapProcess = StartSuite(executablePath, GetBootstrapArguments(port));
				await using var cdpClient = await TrezorSuiteCdpClient.ConnectAsync(
					port,
					SuiteStartupTimeout,
					cancellationToken).ConfigureAwait(false);
				if (state.OriginalAnonymityTargets is { Count: > 0 } originals)
				{
					await cdpClient.EvaluateAsync(TrezorSuitePrivacyScripts.Restore(originals), cancellationToken).ConfigureAwait(false);
				}
				await cdpClient.RestoreDebugSettingsAsync(
					state.OriginalSettingsExisted,
					state.OriginalSettingsJson,
					cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				await StopBootstrapProcessAsync(bootstrapProcess).ConfigureAwait(false);
			}

			File.Delete(_stateFilePath);
			StartSuite(executablePath, GetLaunchArguments()).Dispose();
			return await GetStatusAsync(executablePath, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_operationLock.Release();
		}
	}

	public void Launch(string? preferredPath = null)
	{
		var state = LoadStateAsync(CancellationToken.None).GetAwaiter().GetResult();
		var executablePath = RequireExecutablePath(preferredPath, state?.SuiteExecutablePath);
		StartSuite(executablePath, GetLaunchArguments()).Dispose();
	}

	public void Dispose()
	{
		if (_ownsHttpClient)
		{
			_httpClient.Dispose();
		}
		_operationLock.Dispose();
	}

	internal string[] GetLaunchArguments() =>
	[
		"--log-write",
		"--log-level=debug",
		$"--log-path={LogDirectoryPath}",
		"--log-file=trezor-suite-log-%ts.txt"
	];

	internal string[] GetBootstrapArguments(int port) =>
	[
		.. GetLaunchArguments(),
		"--open-devtools",
		"--remote-debugging-address=127.0.0.1",
		$"--remote-debugging-port={port}"
	];

	private static int GetUnusedLoopbackPort()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		try
		{
			return ((IPEndPoint)listener.LocalEndpoint).Port;
		}
		finally
		{
			listener.Stop();
		}
	}

	private static Process StartSuite(string executablePath, string[] arguments)
	{
		var startInfo = new ProcessStartInfo(executablePath)
		{
			UseShellExecute = false,
			CreateNoWindow = true
		};

		foreach (var argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		return Process.Start(startInfo)
			?? throw new TrezorSuiteIntegrationException("Trezor Suite could not be started.");
	}

	private static async Task StopBootstrapProcessAsync(Process? process)
	{
		if (process is null)
		{
			return;
		}

		try
		{
			if (process.HasExited)
			{
				return;
			}

			if (OperatingSystem.IsWindows())
			{
				process.CloseMainWindow();
			}
			else
			{
				NativeMethods.SendSigTerm(process.Id);
			}

			using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(8));
			try
			{
				await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				process.Kill(entireProcessTree: true);
				await process.WaitForExitAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			process.Dispose();
		}
	}

	private async Task<bool> IsBackendAvailableAsync(CancellationToken cancellationToken)
	{
		using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutSource.CancelAfter(TimeSpan.FromSeconds(10));

		try
		{
			using var versionRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(GingerBackendUri, "api/Software/versions"));
			using var versionResponse = await _httpClient.SendAsync(versionRequest, timeoutSource.Token).ConfigureAwait(false);
			if (!versionResponse.IsSuccessStatusCode)
			{
				return false;
			}

			using var statusRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(GingerBackendUri, "WabiSabi/status"))
			{
				Content = new StringContent("{\"RoundCheckpoints\":[]}", Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"))
			};
			using var statusResponse = await _httpClient.SendAsync(statusRequest, timeoutSource.Token).ConfigureAwait(false);
			return statusResponse.IsSuccessStatusCode;
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
		{
			return false;
		}
	}

	private string RequireExecutablePath(string? preferredPath, string? previouslyUsedPath)
	{
		return _locator.Locate(preferredPath, previouslyUsedPath)
			?? throw new TrezorSuiteIntegrationException("Trezor Suite was not found. Select the Trezor Suite executable and try again.");
	}

	private static string? TryGetSuiteVersion(string? executablePath)
	{
		if (executablePath is null)
		{
			return null;
		}

		try
		{
			var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
			return string.IsNullOrWhiteSpace(versionInfo.ProductVersion)
				? versionInfo.FileVersion
				: versionInfo.ProductVersion;
		}
		catch (Exception ex) when (ex is FileNotFoundException or IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	private async Task<TrezorSuiteIntegrationState?> LoadStateAsync(CancellationToken cancellationToken)
	{
		if (!File.Exists(_stateFilePath))
		{
			return null;
		}

		await using var stream = File.OpenRead(_stateFilePath);
		return await JsonSerializer.DeserializeAsync<TrezorSuiteIntegrationState>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	private async Task SaveStateAsync(TrezorSuiteIntegrationState state, CancellationToken cancellationToken)
	{
		var directory = Path.GetDirectoryName(_stateFilePath)!;
		Directory.CreateDirectory(directory);
		await using var stream = File.Create(_stateFilePath);
		await JsonSerializer.SerializeAsync(stream, state, new JsonSerializerOptions { WriteIndented = true }, cancellationToken).ConfigureAwait(false);
	}

	private static class NativeMethods
	{
		private const int SigTerm = 15;

		[DllImport("libc", EntryPoint = "kill", SetLastError = true)]
		private static extern int Kill(int processId, int signal);

		public static void SendSigTerm(int processId)
		{
			_ = Kill(processId, SigTerm);
		}
	}
}
