using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using GingerCommon.Logging;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Daemon;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.CrashReport;
using WalletWasabi.Fluent.Desktop.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Lang.Models;
using WalletWasabi.Models;
using WalletWasabi.Services.Terminate;

namespace WalletWasabi.Fluent.Desktop;

public class Program
{
	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	public static async Task<int> Main(string[] args)
	{
		// Crash reporting must be before the "single instance checking".
		Logger.CreateLogger(LogLevel.Information, LogLevel.Information, LogLevel.Information, Path.Combine(Config.DataDir, "Logs.txt"));
		try
		{
			if (CrashReporter.TryGetExceptionFromCliArgs(args, out var exceptionToShow))
			{
				// Show the exception.
				BuildCrashReporterApp(exceptionToShow).StartWithClassicDesktopLifetime(args);
				return 1;
			}
		}
		catch (Exception ex)
		{
			// If anything happens here just log it and exit.
			Logger.LogCritical(ex);
			return 1;
		}

		try
		{
			var app = WasabiAppBuilder
				.Create("Ginger GUI", args)
				.EnsureSingleInstance()
				.OnUnhandledExceptions(LogUnhandledException)
				.OnUnobservedTaskExceptions(LogUnobservedTaskException)
				.OnTermination(TerminateApplication)
				.Build();

			var exitCode = await app.RunAsGuiAsync();

			if (app.TerminateService.GracefulCrashException is not null)
			{
				throw app.TerminateService.GracefulCrashException;
			}

			if (exitCode == ExitCode.Ok && Services.UpdateManager.DoUpdateOnClose)
			{
				Services.UpdateManager.StartInstallingNewVersion();
			}

			return (int)exitCode;
		}
		catch (Exception ex)
		{
			CrashReporter.Invoke(ex);
			Logger.LogCritical(ex);
			return 1;
		}
		finally
		{
			try
			{
				Logger.FinishFileLogging();
				if (TerminateService.Instance?.RestartRequest ?? false)
				{
					AppLifetimeHelper.StartAppWithArgs();
				}
			}
			catch (Exception ex)
			{
				Logger.LogCritical(ex);
			}
		}
	}

	/// <summary>
	/// Do not call this method it should only be called by TerminateService.
	/// </summary>
	private static void TerminateApplication()
	{
		Dispatcher.UIThread.Post(() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Close());
	}

	private static void LogUnobservedTaskException(object? sender, AggregateException e)
	{
		ReadOnlyCollection<Exception> innerExceptions = e.Flatten().InnerExceptions;

		switch (innerExceptions)
		{
			case [SocketException { SocketErrorCode: SocketError.OperationAborted }]:
			// Source of this exception is NBitcoin library.
			case [OperationCanceledException { Message: "The peer has been disconnected" }]:
				// Until https://github.com/MetacoSA/NBitcoin/pull/1089 is resolved.
				Logger.LogTrace(e);
				break;

			default:
				Logger.LogDebug(e);
				break;
		}
	}

	private static void LogUnhandledException(object? sender, Exception e) =>
		Logger.LogWarning(e);

	[SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Required to bootstrap Avalonia's Visual Previewer")]
	private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() => new App()).UseReactiveUI().SetupAppBuilder();

	/// <summary>
	/// Sets up and initializes the crash reporting UI.
	/// </summary>
	/// <param name="serializableException">The serializable exception</param>
	private static AppBuilder BuildCrashReporterApp(SerializableException serializableException)
	{
		var result = AppBuilder
			.Configure(() => new CrashReportApp(serializableException))
			.UseReactiveUI();

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			result
				.UseWin32()
				.UseSkia();
		}
		else
		{
			result.UsePlatformDetect();
		}

		return result
			.With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
			.With(new X11PlatformOptions { RenderingMode = new[] { X11RenderingMode.Software }, WmClass = "Wasabi Wallet Crash Report" })
			.With(new AvaloniaNativePlatformOptions { RenderingMode = new[] { AvaloniaNativeRenderingMode.Software } })
			.With(new MacOSPlatformOptions { ShowInDock = true })
			.AfterSetup(_ => ThemeHelper.ApplyTheme(Theme.Dark));
	}
}

public static class WasabiAppExtensions
{
	public static async Task<ExitCode> RunAsGuiAsync(this WasabiApplication app)
	{
		return await app.RunAsync(
			afterStarting: () =>
			{
				RxApp.DefaultExceptionHandler = Observer.Create<Exception>(ex =>
				{
					if (Debugger.IsAttached)
					{
						Debugger.Break();
					}

					Logger.LogError(ex);

					RxApp.MainThreadScheduler.Schedule(() => throw new ApplicationException("Exception has been thrown in unobserved ThrownExceptions", ex));
				});

				Logger.LogInfo("Wasabi GUI started.");
				bool runGuiInBackground = app.AppConfig.Arguments.Any(arg => arg.Contains(StartupHelper.SilentArgument));
				UiConfig uiConfig = LoadOrCreateUiConfig(Config.DataDir);
				uiConfig
					.WhenAnyValue(x => x.SelectedBrowser)
					.Do(x => WebBrowserService.Instance.SetConfig(x))
					.Subscribe();

				Services.Initialize(app.Global!, uiConfig, app.SingleInstanceChecker, app.TerminateService);

				using CancellationTokenSource stopLoadingCts = new();

				AppBuilder appBuilder = AppBuilder
					.Configure(() => new App(
						backendInitialiseAsync: async () =>
						{
							// macOS require that Avalonia is started with the UI thread. Hence this call must be delayed to this point.
							await app.Global!.InitializeNoWalletAsync(initializeSleepInhibitor: true, app.TerminateService, stopLoadingCts.Token).ConfigureAwait(false);

							// Make sure that wallet startup set correctly regarding RunOnSystemStartup
							await StartupHelper.ModifyStartupSettingAsync(uiConfig.RunOnSystemStartup).ConfigureAwait(false);
						},
						startInBg: runGuiInBackground))
					.UseReactiveUI()
					.SetupAppBuilder()
					.AfterSetup(_ =>
					{
						var config = app.Global!.Config;
						Lang.Resources.Culture = new GingerCultureInfo(((DisplayLanguage)config.Language).GetDescription()!)
						{
							BitcoinFractionGroupSizes = config.BtcFractionGroup,
							BitcoinTicker = "BTC",
							FiatTicker = config.ExchangeCurrency,
							NumberFormat =
							{
								NumberGroupSizes = [3],
								CurrencyGroupSeparator = config.GroupSeparator,
								CurrencyDecimalSeparator = config.DecimalSeparator,
								NumberGroupSeparator = config.GroupSeparator,
								NumberDecimalSeparator = config.DecimalSeparator
							}
						};
						ThemeHelper.ApplyTheme(uiConfig.DarkModeEnabled ? Theme.Dark : Theme.Light);
					});

				if (app.TerminateService.CancellationToken.IsCancellationRequested)
				{
					Logger.LogDebug("Skip starting Avalonia UI as requested the application to stop.");
					stopLoadingCts.Cancel();
				}
				else
				{
					appBuilder.StartWithClassicDesktopLifetime(app.AppConfig.Arguments);
				}

				return Task.CompletedTask;
			});
	}

	private static UiConfig LoadOrCreateUiConfig(string dataDir)
	{
		Directory.CreateDirectory(dataDir);

		UiConfig uiConfig = new(Path.Combine(dataDir, "UiConfig.json"));
		uiConfig.LoadFile(createIfMissing: true);

		return uiConfig;
	}
}
