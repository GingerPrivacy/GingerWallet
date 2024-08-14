using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace WalletWasabi.Daemon.Helpers;

public static class TorBrowserHelpers
{
	public static bool IsTorBrowserInstalled(out string torExecutablePath)
	{
		torExecutablePath = string.Empty;

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Tor Browser", "Browser", "firefox.exe");
			if (File.Exists(path))
			{
				torExecutablePath = path;
				return true;
			}
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			string path = "/Applications/Tor Browser.app/Contents/MacOS/firefox";
			if (File.Exists(path))
			{
				torExecutablePath = path;
				return true;
			}
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			string? homeDir = Environment.GetEnvironmentVariable("HOME");
			if (!string.IsNullOrEmpty(homeDir))
			{
				string path = Path.Combine(homeDir, "tor-browser", "Browser", "firefox");
				if (File.Exists(path))
				{
					torExecutablePath = path;
					return true;
				}
			}
		}

		return false;
	}

	public static bool IsTorBrowser(string torExecutablePath)
	{
		if (torExecutablePath.Contains("Tor Browser") || torExecutablePath.Contains("tor-browser"))
		{
			return true;
		}

		return false;
	}

	public static void OpenTorBrowser(string torBrowserExecutablePath, string url)
	{
		if (File.Exists(torBrowserExecutablePath))
		{
			ProcessStartInfo startInfo = new()
			{
				FileName = torBrowserExecutablePath,
				Arguments = url
			};

			try
			{
				Process.Start(startInfo);
				Console.WriteLine($"Tor Browser launched and navigating to {url}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Failed to start Tor Browser: {ex.Message}");
			}
		}
		else
		{
			Console.WriteLine("The specified Tor Browser path does not exist.");
		}
	}
}
