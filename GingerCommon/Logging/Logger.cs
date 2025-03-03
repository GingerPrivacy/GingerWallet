using GingerCommon.Static;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace GingerCommon.Logging;

public static class Logger
{
	public static void CreateLogger(LogLevel? consoleLogLevel = null, LogLevel? debugLogLevel = null, LogLevel? fileLogLevel = null, string? filePath = null, long maximumLogFileSizeBytes = 10_000_000)
	{
		InitLevelStrings();
#if RELEASE
		consoleLogLevel ??= LogLevel.Information;
		debugLogLevel ??= LogLevel.None;
		fileLogLevel ??= LogLevel.Information;
#else
		consoleLogLevel ??= LogLevel.Debug;
		debugLogLevel ??= LogLevel.Debug;
		fileLogLevel ??= LogLevel.Debug;
#endif
		filePath ??= "Logs.txt";
		FilePath = filePath;

		using ILoggerFactory factory = LoggerFactory.Create(builder =>
		{
			builder.ClearProviders();
			if (debugLogLevel != LogLevel.None)
			{
				builder.AddFilter<DebugLoggerProvider>(null, (LogLevel)debugLogLevel);
				builder.AddDebug();
			}
			if (consoleLogLevel != LogLevel.None)
			{
				builder.AddFilter<ConsoleLoggerProvider>(null, (LogLevel)consoleLogLevel);
				builder.AddConsole(options => options.FormatterName = "Console");
				builder.AddConsoleFormatter<ConsoleFormatter, ConsoleFormatterOptions>();
			}
			if (fileLogLevel != LogLevel.None)
			{
				builder.AddFilter<FileLoggerProvider>(null, (LogLevel)fileLogLevel);
				builder.AddProvider(new FileLoggerProvider(filePath, maximumLogFileSizeBytes));
			}
		});
		ILogger logger = factory.CreateLogger("Ginger");
		LoggerInstance = logger;
	}

	public static string FilePath { get; private set; } = "Logs.txt";

	public static bool On { get; set; } = true;

	private static ILogger? LoggerInstance = null;
	private static string[]? LogLevelStrings = null;

	private static void InitLevelStrings()
	{
		if (LogLevelStrings is null)
		{
			LogLevelStrings = new string[1 + (int)LogLevel.None];
			for (LogLevel level = LogLevel.Trace; level <= LogLevel.None; level++)
			{
				LogLevelStrings[(int)level] = $"{level.ToString().ToUpperInvariant(),-7}";
			}
			LogLevelStrings[(int)LogLevel.Information] = "INFO   ";
		}
	}

	public static string GetLevelString(LogLevel level)
	{
		var logLevelString = LogLevelStrings;
		return (logLevelString is not null && level >= LogLevel.Trace && level <= LogLevel.None ? logLevelString[(int)level] : null) ?? "UNKNOWN";
	}

	public sealed class NullScope : IDisposable
	{
		public static NullScope Instance { get; } = new NullScope();

		private NullScope()
		{
		}

		public void Dispose()
		{
		}
	}

	/// Original logger methods

	public static void Log(LogLevel level, string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		try
		{
			var loggerInstance = LoggerInstance;
			if (loggerInstance is null || !On || !loggerInstance.IsEnabled(level))
			{
				return;
			}

			message = message.SafeTrim();
			var category = string.IsNullOrWhiteSpace(callerFilePath) ? "" : $"{callerFilePath.ExtractFileName()}.{callerMemberName} ({callerLineNumber})";

			var messageBuilder = new StringBuilder();
			messageBuilder.Append(CultureInfo.InvariantCulture, $"{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} [{Environment.CurrentManagedThreadId,2}] {GetLevelString(level)} ");

			messageBuilder.Append(category);
			if (message.Length > 0 && category.Length > 0)
			{
				messageBuilder.Append('\t');
			}
			messageBuilder.Append(message);

			var finalMessage = messageBuilder.ToString();
			loggerInstance.Log(level, finalMessage);
		}
		catch (Exception ex)
		{
			if (Interlocked.Increment(ref LoggingFailedCount) == 1) // If it only failed the first time, try log the failure.
			{
				LogDebug($"Logging failed: {GetEnglishExceptionString(ex)}");
			}

			// If logging the failure is successful then clear the failure counter.
			// If it's not the first time the logging failed, then we do not try to log logging failure, so clear the failure counter.
			Interlocked.Exchange(ref LoggingFailedCount, 0);
		}
	}

	/// <summary>
	/// Logs user message concatenated with exception string.
	/// </summary>
	public static void Log(string message, Exception ex, LogLevel level, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(level, message: $"{message} Exception: {GetEnglishExceptionString(ex)}", callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	/// <summary>
	/// Logs exception string without any user message.
	/// </summary>
	public static void Log(Exception exception, LogLevel level, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(level, GetEnglishExceptionString(exception), callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	private static int LoggingFailedCount = 0;

	/// <summary>
	/// Gets the GUID instance.
	/// <para>You can use it to identify which software instance created a log entry. It gets created automatically, but you have to use it manually.</para>
	/// </summary>
	private static Guid InstanceGuid { get; } = Guid.NewGuid();

	private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

	private static string GetEnglishExceptionString(Exception ex)
	{
		var originalCulture = Thread.CurrentThread.CurrentUICulture;
		try
		{
			// Set the culture to English for logging
			Thread.CurrentThread.CurrentUICulture = EnglishCulture;
			return ex.ToString();
		}
		finally
		{
			// Ensure the original culture is always restored
			Thread.CurrentThread.CurrentUICulture = originalCulture;
		}
	}

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Trace"/> level.
	/// <para>For information that is valuable only to a developer debugging an issue.</para>
	/// </summary>
	/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
	/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
	public static void LogTrace(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Trace, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Trace"/> level.
	/// <para>For information that is valuable only to a developer debugging an issue.</para>
	/// </summary>
	/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
	/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
	public static void LogTrace(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Trace, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs <paramref name="message"/> with <paramref name="exception"/> using <see cref="Exception.ToString()"/> concatenated to it at <see cref="LogLevel.Trace"/> level.
	/// <para>For information that is valuable only to a developer debugging an issue.</para>
	/// </summary>
	/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
	/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
	public static void LogTrace(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Log(message, exception, LogLevel.Trace, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Debug"/> level.
	/// <para>For information that has short-term usefulness during development and debugging.</para>
	/// </summary>
	/// <remarks>You typically would not enable <see cref="LogLevel.Debug"/> level in production unless you are troubleshooting, due to the high volume of generated logs.</remarks>
	/// <example>For example: <c>Entering method Configure with flag set to true.</c></example>
	public static void LogDebug(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Debug, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Debug"/> level.
	/// <para>For information that is valuable only to a developer debugging an issue.</para>
	/// </summary>
	/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
	/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
	public static void LogDebug(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Debug, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs <paramref name="message"/> with <paramref name="exception"/> using <see cref="Exception.ToString()"/> concatenated to it at <see cref="LogLevel.Debug"/> level.
	/// <para>For information that has short-term usefulness during development and debugging.</para>
	/// </summary>
	/// <remarks>You typically would not enable <see cref="LogLevel.Debug"/> level in production unless you are troubleshooting, due to the high volume of generated logs.</remarks>
	public static void LogDebug(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Log(message, exception, LogLevel.Debug, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs special event: Software has started. Add also <see cref="InstanceGuid"/> identifier and insert three newlines to increase log readability.
	/// </summary>
	/// <param name="appName">Name of the application.</param>
	public static void LogSoftwareStarted(string appName, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		// We add the extra empty lines to all log outputs
		LoggerInstance?.Log(LogLevel.Information, "\n\n\n");
		Log(LogLevel.Information, $"{appName} started ({InstanceGuid}).", callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	/// <summary>
	/// Logs special event: Software has stopped. Add also <see cref="InstanceGuid"/> identifier.
	/// </summary>
	/// <param name="appName">Name of the application.</param>
	public static void LogSoftwareStopped(string appName, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Log(LogLevel.Information, $"{appName} stopped gracefully ({InstanceGuid}).", callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Information"/> level.
	/// <para>For tracking the general flow of the application.</para>
	/// <remarks>These logs typically have some long-term value.</remarks>
	/// <example>"Request received for path /api/my-controller"</example>
	/// </summary>
	public static void LogInfo(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Information, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Information"/> level.
	/// <para>For tracking the general flow of the application.</para>
	/// These logs typically have some long-term value.
	/// </summary>
	public static void LogInfo(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Information, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs <paramref name="message"/> with <paramref name="exception"/> using <see cref="Exception.ToString()"/> concatenated to it at <see cref="LogLevel.Information"/> level.
	/// <para>For tracking the general flow of the application.</para>
	/// These logs typically have some long-term value.
	/// </summary>
	public static void LogInfo(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Log(message, exception, LogLevel.Information, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Warning"/> level.
	/// <para>For abnormal or unexpected events in the application flow.</para>
	/// <remarks>
	/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
	/// Handled exceptions are a common place to use the Warning log level.
	/// </remarks>
	/// </summary>
	public static void LogWarning(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Warning, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Warning"/> level.
	/// <para>For abnormal or unexpected events in the application flow.</para>
	/// </summary>
	/// <remarks>
	/// <para>Includes situations when errors or other conditions occur that do not cause the application to stop, but which may need to be investigated.</para>
	/// <para>Handled exceptions are a common place to use the <see cref="LogLevel.Warning"/> log level.</para>
	/// </remarks>
	public static void LogWarning(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Warning, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Error"/> level.
	/// <para>For errors and exceptions that cannot be handled.</para>
	/// </summary>
	/// <remarks>These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.</remarks>
	public static void LogError(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Error, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs <paramref name="message"/> with <paramref name="exception"/> using <see cref="Exception.ToString()"/> concatenated to it at <see cref="LogLevel.Error"/> level.
	/// <para>For errors and exceptions that cannot be handled.</para>
	/// </summary>
	/// <remarks>These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.</remarks>
	public static void LogError(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Log(message, exception, LogLevel.Error, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Error"/> level.
	/// <para>For errors and exceptions that cannot be handled.</para>
	/// </summary>
	/// <remarks>These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.</remarks>
	public static void LogError(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Error, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Critical"/> level.
	/// <para>For failures that require immediate attention.</para>
	/// </summary>
	/// <example>Data loss scenarios, out of disk space.</example>
	public static void LogCritical(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Critical, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Critical"/> level.
	/// <para>For failures that require immediate attention.</para>
	/// </summary>
	/// <example>Examples: Data loss scenarios, out of disk space, etc.</example>
	public static void LogCritical(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Critical, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
}
