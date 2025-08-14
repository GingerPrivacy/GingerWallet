using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent;

public sealed class WebBrowserService
{
	private const string InternetExplorerDefaultPath = @"C:\Program Files\Internet Explorer\iexplore.exe";
	private static Lazy<WebBrowserService> LazyInstance { get; } = new Lazy<WebBrowserService>(() => new WebBrowserService());

	private string? CustomBrowserPath { get; set; } = null;
	private BrowserType? PreferredBrowserType { get; set; } = null;

	private WebBrowserService()
	{
	}

	public static WebBrowserService Instance
	{
		get { return LazyInstance.Value; }
	}

	public void SetConfig(string filePathOrEnumValueThatIsInConfig)
	{
		CustomBrowserPath = null;
		PreferredBrowserType = null;

		if (Enum.TryParse(filePathOrEnumValueThatIsInConfig, true, out BrowserType preferredBrowserType))
		{
			PreferredBrowserType = preferredBrowserType;
		}
		else if (!string.IsNullOrWhiteSpace(filePathOrEnumValueThatIsInConfig))
		{
			CustomBrowserPath = filePathOrEnumValueThatIsInConfig;
		}
	}

	public async Task OpenUrlInPreferredBrowserAsync(string url)
	{
		if (PreferredBrowserType is BrowserType.Tor || IsLikelyTorPath(CustomBrowserPath))
		{
			if (url.Contains("mempool.space"))
			{
				url = url.Replace("https://mempool.space", "http://mempoolhqx4isw62xs7abwphsq7ldayuidyx2v2oethdhhj6mlo2r6ad.onion");
			}
		}

		// First priority: Custom browser path
		if (!string.IsNullOrWhiteSpace(CustomBrowserPath))
		{
			OpenInBrowser(url, CustomBrowserPath);
			return;
		}

		// Second priority: Preferred browser if specified
		if (PreferredBrowserType.HasValue)
		{
			if (TryOpenPreferredBrowser(url, PreferredBrowserType.Value))
			{
				return;
			}
			else
			{
				throw new InvalidOperationException($"Preferred browser set to: '{PreferredBrowserType}' but it fails with an error.");
			}
		}

		// Default browser logic based on OS

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			// If no associated application/json MimeType is found xdg-open opens return error
			// but it tries to open it anyway using the console editor (nano, vim, other..)
			await EnvironmentHelpers.ShellExecAsync($"xdg-open {url}", waitForExit: false).ConfigureAwait(false);
		}
		else
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				url = url.Replace(" ", "\\ ");

				await EnvironmentHelpers.ShellExecAsync($"open {url}").ConfigureAwait(false);
			}
			else
			{
				using var process = Process.Start(new ProcessStartInfo
				{
					FileName = url,
					CreateNoWindow = true,
					UseShellExecute = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				});
			}
		}
	}

	private static bool TryOpenPreferredBrowser(string url, BrowserType preferredBrowser)
	{
		switch (preferredBrowser)
		{
			case BrowserType.Tor:
				return TryOpenTorBrowser(url);

			case BrowserType.Chrome:
				return TryOpenChrome(url);

			case BrowserType.Brave:
				return TryOpenBrave(url);

			case BrowserType.Opera:
				return TryOpenOpera(url);

			case BrowserType.InternetExplorer:
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					string iePath = InternetExplorerDefaultPath;
					if (File.Exists(iePath))
					{
						Process.Start(iePath, url);
						return true;
					}

					return true;
				}

				break;

			case BrowserType.Safari:
				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					OpenInBrowser(url, "open"); // Safari uses the 'open' command
					return true;
				}

				break;

			case BrowserType.Firefox:
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					OpenInBrowser(url, GetFirefoxPath());
					return true;
				}

				break;
		}

		return false;
	}

	private static bool TryOpenTorBrowser(string url)
	{
		string torBrowserPath = GetTorBrowserPath();
		if (!string.IsNullOrEmpty(torBrowserPath) && File.Exists(torBrowserPath))
		{
			Process.Start(torBrowserPath, url);
			return true;
		}

		return false;
	}

	private static bool TryOpenChrome(string url)
	{
		string chromePath = GetChromePath();
		if (!string.IsNullOrEmpty(chromePath) && File.Exists(chromePath))
		{
			Process.Start(chromePath, url);
			return true;
		}

		return false;
	}

	private static bool TryOpenBrave(string url)
	{
		string bravePath = GetBravePath();
		if (!string.IsNullOrEmpty(bravePath) && File.Exists(bravePath))
		{
			Process.Start(bravePath, url);
			return true;
		}

		return false;
	}

	private static bool TryOpenOpera(string url)
	{
		string operaPath = GetOperaPath();
		if (!string.IsNullOrEmpty(operaPath) && File.Exists(operaPath))
		{
			Process.Start(operaPath, url);
			return true;
		}

		return false;
	}

	private static void OpenInBrowser(string url, string browserPath)
	{
		Process.Start(browserPath, url);
	}

	private static string GetTorBrowserPath()
	{
		string username = Environment.UserName;

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return $@"C:\Users\{username}\Desktop\Tor Browser\Browser\firefox.exe";
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			return @"/Applications/Tor Browser.app/Contents/MacOS/firefox";
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "", "tor-browser", "Browser", "firefox");
		}

		return string.Empty;
	}

	private static string GetChromePath()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return @"C:\Program Files\Google\Chrome\Application\chrome.exe";
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			return @"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return "/usr/bin/google-chrome";
		}

		return string.Empty;
	}

	private static string GetBravePath()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe";
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			return @"/Applications/Brave Browser.app/Contents/MacOS/Brave Browser";
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return "/usr/bin/brave-browser";
		}

		return string.Empty;
	}

	private static string GetOperaPath()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return @"C:\Users\YourUsername\AppData\Local\Programs\Opera\launcher.exe";
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			return @"/Applications/Opera.app/Contents/MacOS/Opera";
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return "/usr/bin/opera";
		}

		return string.Empty;
	}

	private static string GetFirefoxPath()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return @"C:\Program Files\Mozilla Firefox\firefox.exe";
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return "/usr/bin/firefox";
		}

		return string.Empty;
	}

	public static List<BrowserType> GetAvailableBrowsers()
	{
		var availableBrowsers = new List<BrowserType>();

		if (!string.IsNullOrEmpty(GetTorBrowserPath()) && File.Exists(GetTorBrowserPath()))
		{
			availableBrowsers.Add(BrowserType.Tor);
		}

		if (!string.IsNullOrEmpty(GetChromePath()) && File.Exists(GetChromePath()))
		{
			availableBrowsers.Add(BrowserType.Chrome);
		}

		if (!string.IsNullOrEmpty(GetBravePath()) && File.Exists(GetBravePath()))
		{
			availableBrowsers.Add(BrowserType.Brave);
		}

		if (!string.IsNullOrEmpty(GetOperaPath()) && File.Exists(GetOperaPath()))
		{
			availableBrowsers.Add(BrowserType.Opera);
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(InternetExplorerDefaultPath))
		{
			availableBrowsers.Add(BrowserType.InternetExplorer);
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && File.Exists("/Applications/Safari.app"))
		{
			availableBrowsers.Add(BrowserType.Safari);
		}

		if (!string.IsNullOrEmpty(GetFirefoxPath()) && File.Exists(GetFirefoxPath()))
		{
			availableBrowsers.Add(BrowserType.Firefox);
		}

		return availableBrowsers;
	}

	private static bool IsLikelyTorPath(string? customBrowserPath)
	{
		if (string.IsNullOrWhiteSpace(customBrowserPath))
		{
			return false;
		}

		if (customBrowserPath.Contains("firefox") &&
		    customBrowserPath.Contains("tor", StringComparison.InvariantCultureIgnoreCase) &&
		    customBrowserPath.Contains("browser", StringComparison.InvariantCultureIgnoreCase))
		{
			return true;
		}

		return false;
	}
}

public enum BrowserType
{
	Tor,
	Chrome,
	Brave,
	Opera,
	InternetExplorer,
	Safari,
	Firefox
}
