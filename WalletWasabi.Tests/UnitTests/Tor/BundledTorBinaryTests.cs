using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Microservices;
using WalletWasabi.Tor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor;

public class BundledTorBinaryTests
{
	[Fact]
	public async Task BundledTorBinaryCanReportVersionAsync()
	{
		string torBinaryDir = Path.Combine(MicroserviceHelpers.GetBinaryFolder(), "Tor");
		string torBinaryPath = TorSettings.GetTorBinaryFilePath(torBinaryDir);

		Assert.True(File.Exists(torBinaryPath), $"Bundled Tor binary was not found at '{torBinaryPath}'.");

		using Process process = new()
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = torBinaryPath,
				Arguments = "--version",
				WorkingDirectory = torBinaryDir,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			}
		};

		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			string existingLdLibraryPath = process.StartInfo.Environment.TryGetValue("LD_LIBRARY_PATH", out var value) ? value ?? "" : "";
			process.StartInfo.Environment["LD_LIBRARY_PATH"] =
				string.IsNullOrWhiteSpace(existingLdLibraryPath)
					? torBinaryDir
					: $"{torBinaryDir}{Path.PathSeparator}{existingLdLibraryPath}";
		}

		process.Start();

		Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
		Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();

		if (!process.WaitForExit(milliseconds: 15_000))
		{
			process.Kill(entireProcessTree: true);
			throw new TimeoutException("Bundled Tor binary did not exit when queried for its version.");
		}

		string standardOutput = await standardOutputTask;
		string standardError = await standardErrorTask;
		string output = $"{standardOutput}{standardError}";
		Assert.Equal(0, process.ExitCode);
		Assert.Contains("Tor version ", output, StringComparison.Ordinal);
	}
}
