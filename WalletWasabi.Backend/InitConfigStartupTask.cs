using GingerCommon.Logging;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Backend;

public class InitConfigStartupTask : IStartupTask
{
	public InitConfigStartupTask(Global global)
	{
		Global = global;
	}

	private Global Global { get; }

	public async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		Logger.CreateLogger(filePath: Path.Combine(Global.DataDir, "Logs.txt"));
		Global.CreateDiscordLogger();

		Logger.LogSoftwareStarted("Ginger Backend");
		Logger.LogDiscord(LogLevel.Information, "Ginger Backend started");

		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

		await Global.InitializeAsync(cancellationToken);
	}

	private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		Logger.LogDebug(e.Exception);
	}

	private static void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			Logger.LogWarning(ex);
		}
	}
}
