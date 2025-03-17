using LinqKit;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using Constants = WalletWasabi.Helpers.Constants;

namespace WalletWasabi.Daemon;

public class WasabiApplication
{
	public WasabiAppBuilder AppConfig { get; }
	public Global? Global { get; private set; }
	public string ConfigFilePath { get; }
	public Config Config { get; }
	public SingleInstanceChecker SingleInstanceChecker { get; }
	public TerminateService TerminateService { get; }

	public WasabiApplication(WasabiAppBuilder wasabiAppBuilder)
	{
		AppConfig = wasabiAppBuilder;

		ConfigFilePath = Path.Combine(Config.DataDir, "Config.json");
		// GingerWallet folder will exist already due to the logger, we check the config file instead
		bool migrated = !File.Exists(ConfigFilePath) && MigrateDataFromWasabiWallet();

		Directory.CreateDirectory(Config.DataDir);
		Config = new Config(LoadOrCreateConfigs(migrated), wasabiAppBuilder.Arguments);

		SetupLogger();
		Logger.LogDebug($"Ginger Wallet was started with these argument(s): {string.Join(" ", AppConfig.Arguments.DefaultIfEmpty("none"))}.");
		SingleInstanceChecker = new(Config.Network);
		TerminateService = new(TerminateApplicationAsync, AppConfig.Terminate);
	}

	public async Task<ExitCode> RunAsync(Func<Task> afterStarting)
	{
		if (AppConfig.Arguments.Contains("--version"))
		{
			Console.WriteLine($"{AppConfig.AppName} {Constants.ClientVersion}");
			return ExitCode.Ok;
		}
		if (AppConfig.Arguments.Contains("--help"))
		{
			ShowHelp();
			return ExitCode.Ok;
		}

		if (AppConfig.MustCheckSingleInstance)
		{
			var instanceResult = await SingleInstanceChecker.CheckSingleInstanceAsync();
			if (instanceResult == WasabiInstanceStatus.AnotherInstanceIsRunning)
			{
				Logger.LogDebug("Ginger Wallet is already running, signaled the first instance.");
				return ExitCode.FailedAlreadyRunningSignaled;
			}
			if (instanceResult == WasabiInstanceStatus.Error)
			{
				Logger.LogCritical($"Ginger Wallet is already running, but cannot be signaled");
				return ExitCode.FailedAlreadyRunningError;
			}
		}

		try
		{
			TerminateService.Activate();

			BeforeStarting();

			await afterStarting();
			return ExitCode.Ok;
		}
		finally
		{
			BeforeStopping();
		}
	}

	private void BeforeStarting()
	{
		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

		Logger.LogSoftwareStarted(AppConfig.AppName);

		Global = CreateGlobal();
	}

	private void BeforeStopping()
	{
		AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

		// Start termination/disposal of the application.
		TerminateService.Terminate();
		SingleInstanceChecker.Dispose();
		Logger.LogSoftwareStopped(AppConfig.AppName);
	}

	private Global CreateGlobal()
		=> new(Config.DataDir, ConfigFilePath, Config);

	private PersistentConfig LoadOrCreateConfigs(bool migrated)
	{
		var persistentConfig = ConfigManagerNg.LoadFile<PersistentConfig>(ConfigFilePath, createIfMissing: true);
		if (PersistentConfig.Migrate(migrated, ref persistentConfig))
		{
			Logger.LogInfo("There were changes on the config file to meet the minimum requirements.");
		}

		// Make sure all values visible in the config file, so it can be changed by the users.
		ConfigManagerNg.ToFile(ConfigFilePath, persistentConfig);
		return persistentConfig;
	}

	private bool MigrateDataFromWasabiWallet()
	{
		string wasabiDir = EnvironmentHelpers.GetUncachedDataDir(Path.Combine("WalletWasabi", "Client"), false);
		if (Directory.Exists(wasabiDir))
		{
			// Copy only the important ones
			DateTime start = DateTime.UtcNow;
			Logger.LogInfo($"Copying data from {wasabiDir} to {Config.DataDir}");
			CopyRecursive(wasabiDir, Config.DataDir, "BitcoinP2pNetwork");
			CopyRecursive(wasabiDir, Config.DataDir, "BitcoinStore");
			CopyRecursive(wasabiDir, Config.DataDir, "Legal2");
			CopyRecursive(wasabiDir, Config.DataDir, "Wallets");
			CopyRecursive(wasabiDir, Config.DataDir, "UiConfig.json");
			CopyRecursive(wasabiDir, Config.DataDir, "Config.json");
			Logger.LogInfo($"Copying the Wasabi working folder took {(DateTime.UtcNow - start).TotalSeconds:F2} seconds.");
			return true;
		}

		return false;
	}

	private void CopyRecursive(string srcDir, string destDir, string sub)
	{
		string src = Path.Combine(srcDir, sub);
		string dest = Path.Combine(destDir, sub);
		try
		{
			if (File.Exists(src))
			{
				if (File.Exists(dest))
				{
					FileInfo srcInfo = new(src);
					FileInfo dstInfo = new(dest);
					if (srcInfo.Length != dstInfo.Length)
					{
						File.Delete(dest);
						File.Copy(src, dest);
					}
				}
				else
				{
					File.Copy(src, dest);
				}
			}
			else if (Directory.Exists(src))
			{
				DirectoryInfo info = new(src);
				if (!Directory.Exists(dest))
				{
					Directory.CreateDirectory(dest);
				}
				info.EnumerateDirectories().ForEach(x => CopyRecursive(src, dest, x.Name));
				info.EnumerateFiles().ForEach(x => CopyRecursive(src, dest, x.Name));
			}
		}
		catch (Exception ex)
		{
			Logger.LogInfo($"During the migration couldn't copy the file from {src} to {dest}: {ex.Message}");
		}
	}

	private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		AppConfig.UnobservedTaskExceptionsEventHandler?.Invoke(this, e.Exception);
	}

	private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			AppConfig.UnhandledExceptionEventHandler?.Invoke(this, ex);
		}
	}

	private async Task TerminateApplicationAsync()
	{
		Logger.LogSoftwareStopped(AppConfig.AppName);

		if (Global is { } global)
		{
			await global.DisposeAsync().ConfigureAwait(false);
		}
		await SingleInstanceChecker.StopCheckingAsync();
	}

	private void SetupLogger()
	{
		LogLevel logLevel = Enum.TryParse(Config.LogLevel, ignoreCase: true, out LogLevel parsedLevel)
			? parsedLevel
			: LogLevel.Info;

		Logger.InitializeDefaults(Path.Combine(Config.DataDir, "Logs.txt"), logLevel, Config.LogModes);
	}

	private void ShowHelp()
	{
		Console.WriteLine($"{AppConfig.AppName} {Constants.ClientVersion}");
		Console.WriteLine($"Usage: {AppConfig.AppName} [OPTION]...");
		Console.WriteLine();
		Console.WriteLine("Available options are:");

		foreach (var (parameter, hint) in Config.GetConfigOptionsMetadata().OrderBy(x => x.ParameterName))
		{
			Console.Write($"  --{parameter.ToLowerInvariant(),-30} ");
			var hintLines = hint.SplitLines(lineWidth: 40);
			Console.WriteLine(hintLines[0]);
			foreach (var hintLine in hintLines.Skip(1))
			{
				Console.WriteLine($"{' ',-35}{hintLine}");
			}
			Console.WriteLine();
		}
	}
}
