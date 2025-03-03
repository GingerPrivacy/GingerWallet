using GingerLogger = GingerCommon.Logging.Logger;
using MSLogLevel = Microsoft.Extensions.Logging.LogLevel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace WalletWasabi.Logging;

/// <summary>
/// Logging class.
///
/// <list type="bullet">
/// <item>Logger is enabled by default but no <see cref="Modes"/> are set by default, so the logger does not log by default.</item>
/// <item>Only <see cref="LogLevel.Critical"/> messages are logged unless set otherwise.</item>
/// <item>The logger is thread-safe.</item>
/// </list>
/// </summary>
public static class Logger
{
	/// <summary>
	/// Initializes the logger with default values.
	/// <para>
	/// Default values are set as follows:
	/// <list type="bullet">
	/// <item>For RELEASE mode: MinimumLevel is set to <see cref="LogLevel.Info"/>, and logs only to file.</item>
	/// <item>For DEBUG mode: MinimumLevel is set to <see cref="LogLevel.Debug"/>, and logs to file, debug and console.</item>
	/// </list>
	/// </para>
	/// </summary>
	/// <param name="logLevel">Use <c>null</c> to use default <see cref="LogLevel"/> or a custom value to force non-default <see cref="LogLevel"/>.</param>
	/// <param name="logModes">Use <c>null</c> to use default <see cref="LogMode">logging modes</see> or custom values to force non-default logging modes.</param>
	public static void InitializeDefaults(string filePath, LogLevel? logLevel = null, LogMode[]? logModes = null)
	{
#if RELEASE
		logLevel ??= LogLevel.Info;
		logModes ??= [LogMode.Console, LogMode.File];
#else
		logLevel ??= LogLevel.Debug;
		logModes ??= [LogMode.Debug, LogMode.Console, LogMode.File];
#endif

		long maximumLogFileSizeBytes = 10_000_000;
		if (logLevel == LogLevel.Trace)
		{
			maximumLogFileSizeBytes = 0;
		}

		MSLogLevel msLogLevel = logLevel?.ToMSLogLevel() ?? MSLogLevel.None;

		var consoleLogLevel = logModes.Contains(LogMode.Console) ? msLogLevel : MSLogLevel.None;
		var debugLogLevel = logModes.Contains(LogMode.Debug) ? msLogLevel : MSLogLevel.None;
		var fileLogLevel = logModes.Contains(LogMode.File) ? msLogLevel : MSLogLevel.None;
		GingerLogger.CreateLogger(consoleLogLevel, debugLogLevel, fileLogLevel, filePath, maximumLogFileSizeBytes);
	}

	private static MSLogLevel ToMSLogLevel(this LogLevel logLevel) => (MSLogLevel)logLevel;

	public static void TurnOff() => GingerLogger.On = false;

	public static void TurnOn() => GingerLogger.On = true;

	public static bool IsOn() => GingerLogger.On;

	public static void Log(LogLevel level, string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) =>
		GingerLogger.Log(level.ToMSLogLevel(), message, callerFilePath, callerMemberName, callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Trace"/> level.
	///
	/// <para>For information that is valuable only to a developer debugging an issue.</para>
	/// </summary>
	/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
	/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
	public static void LogTrace(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => GingerLogger.Log(MSLogLevel.Trace, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Trace"/> level.
	///
	/// <para>For information that is valuable only to a developer debugging an issue.</para>
	/// </summary>
	/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
	/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
	public static void LogTrace(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => GingerLogger.Log(exception, MSLogLevel.Trace, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs <paramref name="message"/> with <paramref name="exception"/> using <see cref="Exception.ToString()"/> concatenated to it at <see cref="LogLevel.Trace"/> level.
	///
	/// <para>For information that is valuable only to a developer debugging an issue.</para>
	/// </summary>
	/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
	/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
	public static void LogTrace(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> GingerLogger.Log(message, exception, MSLogLevel.Trace, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Debug"/> level.
	///
	/// <para>For information that has short-term usefulness during development and debugging.</para>
	/// </summary>
	/// <remarks>You typically would not enable <see cref="LogLevel.Debug"/> level in production unless you are troubleshooting, due to the high volume of generated logs.</remarks>
	/// <example>For example: <c>Entering method Configure with flag set to true.</c></example>
	public static void LogDebug(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => GingerLogger.Log(MSLogLevel.Debug, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Debug"/> level.
	///
	/// <para>For information that is valuable only to a developer debugging an issue.</para>
	/// </summary>
	/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
	/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
	public static void LogDebug(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => GingerLogger.Log(exception, MSLogLevel.Debug, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs <paramref name="message"/> with <paramref name="exception"/> using <see cref="Exception.ToString()"/> concatenated to it at <see cref="LogLevel.Debug"/> level.
	///
	/// <para>For information that has short-term usefulness during development and debugging.</para>
	/// </summary>
	/// <remarks>You typically would not enable <see cref="LogLevel.Debug"/> level in production unless you are troubleshooting, due to the high volume of generated logs.</remarks>
	public static void LogDebug(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> GingerLogger.Log(message, exception, MSLogLevel.Debug, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs special event: Software has started. Add also <see cref="InstanceGuid"/> identifier and insert three newlines to increase log readability.
	/// </summary>
	/// <param name="appName">Name of the application.</param>
	public static void LogSoftwareStarted(string appName, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> GingerLogger.LogSoftwareStarted(appName, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs special event: Software has stopped. Add also <see cref="InstanceGuid"/> identifier.
	/// </summary>
	/// <param name="appName">Name of the application.</param>
	public static void LogSoftwareStopped(string appName, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> GingerLogger.LogSoftwareStopped(appName, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Info"/> level.
	///
	/// <para>For tracking the general flow of the application.</para>
	/// <remarks>These logs typically have some long-term value.</remarks>
	/// <example>"Request received for path /api/my-controller"</example>
	/// </summary>
	public static void LogInfo(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => GingerLogger.Log(MSLogLevel.Information, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Info"/> level.
	///
	/// <para>For tracking the general flow of the application.</para>
	/// These logs typically have some long-term value.
	/// Example: "Request received for path /api/my-controller"
	/// </summary>
	public static void LogInfo(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => GingerLogger.Log(exception, MSLogLevel.Information, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs <paramref name="message"/> with <paramref name="exception"/> using <see cref="Exception.ToString()"/> concatenated to it at <see cref="LogLevel.Info"/> level.
	///
	/// <para>For tracking the general flow of the application.</para>
	/// These logs typically have some long-term value.
	/// Example: "Request received for path /api/my-controller"
	/// </summary>
	public static void LogInfo(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> GingerLogger.Log(message, exception, MSLogLevel.Information, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Warning"/> level.
	///
	/// <para>For abnormal or unexpected events in the application flow.</para>
	/// <remarks>
	/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
	/// Handled exceptions are a common place to use the Warning log level.
	/// </remarks>
	/// <example>"FileNotFoundException for file quotes.txt."</example>
	/// </summary>
	public static void LogWarning(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => GingerLogger.Log(MSLogLevel.Warning, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Warning"/> level.
	///
	/// <para>For abnormal or unexpected events in the application flow.</para>
	/// </summary>
	/// <remarks>
	/// <para>Includes situations when errors or other conditions occur that do not cause the application to stop, but which may need to be investigated.</para>
	/// <para>Handled exceptions are a common place to use the <see cref="LogLevel.Warning"/> log level.</para>
	/// </remarks>
	/// <example>For example: <c>FileNotFoundException for file quotes.txt.</c></example>
	public static void LogWarning(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => GingerLogger.Log(exception, MSLogLevel.Warning, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Error"/> level.
	///
	/// <para>For errors and exceptions that cannot be handled.</para>
	/// </summary>
	/// <remarks>These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.</remarks>
	/// <example>Log message such as: "Cannot insert record due to duplicate key violation."</example>
	public static void LogError(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => GingerLogger.Log(MSLogLevel.Error, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs <paramref name="message"/> with <paramref name="exception"/> using <see cref="Exception.ToString()"/> concatenated to it at <see cref="LogLevel.Error"/> level.
	///
	/// <para>For errors and exceptions that cannot be handled.</para>
	/// </summary>
	/// <remarks>These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.</remarks>
	/// <example>Log message such as: "Cannot insert record due to duplicate key violation."</example>
	public static void LogError(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> GingerLogger.Log(message, exception, MSLogLevel.Error, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Error"/> level.
	///
	/// <para>For errors and exceptions that cannot be handled.</para>
	/// </summary>
	/// <remarks>These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.</remarks>
	/// <example>Log message such as: "Cannot insert record due to duplicate key violation."</example>
	public static void LogError(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => GingerLogger.Log(exception, MSLogLevel.Error, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Critical"/> level.
	///
	/// <para>For failures that require immediate attention.</para>
	/// </summary>
	/// <example>Data loss scenarios, out of disk space.</example>
	public static void LogCritical(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => GingerLogger.Log(MSLogLevel.Critical, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Critical"/> level.
	///
	/// <para>For failures that require immediate attention.</para>
	/// </summary>
	/// <example>Examples: Data loss scenarios, out of disk space, etc.</example>
	public static void LogCritical(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => GingerLogger.Log(exception, MSLogLevel.Critical, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
}
