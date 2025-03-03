using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.IO;

namespace GingerCommon.Logging;

/// <summary>
/// Strongly simplified ConsoleFormatter since the message should contain most of the data by the time we get to this class.
/// </summary>
public class ConsoleFormatter : Microsoft.Extensions.Logging.Console.ConsoleFormatter
{
	public ConsoleFormatter() : base("Console")
	{
	}

	public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
	{
		// Needs Logging 9.0
		//if (logEntry.State is BufferedLogRecord bufferedRecord)
		//{
		//	string message = bufferedRecord.FormattedMessage ?? string.Empty;
		//	WriteInternal(null, textWriter, message, bufferedRecord.LogLevel, bufferedRecord.EventId.Id, bufferedRecord.Exception, logEntry.Category);
		//}
		//else
		{
			string message = logEntry.Formatter(logEntry.State, logEntry.Exception);
			if (logEntry.Exception == null && message == null)
			{
				return;
			}

			WriteInternal(scopeProvider, textWriter, message, logEntry.LogLevel, logEntry.EventId.Id, logEntry.Exception?.ToString(), logEntry.Category);
		}
	}

	private void WriteInternal(IExternalScopeProvider? scopeProvider, TextWriter textWriter, string message, LogLevel logLevel,
		int eventId, string? exception, string category)
	{
		string logLevelColors = GetLogLevelConsoleColors(logLevel);
		WriteColoredMessage(textWriter, message, logLevelColors);
	}

	private string GetLogLevelConsoleColors(LogLevel logLevel)
	{
		// Foreground and background colors
		return logLevel switch
		{
			LogLevel.Warning => "\u001B[1m\u001B[33m\u001B[49m",
			LogLevel.Error => "\u001B[1m\u001B[31m\u001B[49m",
			LogLevel.Critical => "\u001B[1m\u001B[31m\u001B[49m",
			_ => ""
		};
	}

	public static void WriteColoredMessage(TextWriter textWriter, string message, string colors)
	{
		if (!string.IsNullOrEmpty(message))
		{
			if (colors.Length > 0)
			{
				textWriter.Write(colors);
			}
			textWriter.Write(message);
			if (colors.Length > 0)
			{
				// Reset colors
				textWriter.Write("\u001B[39m\u001B[22m\u001B[49m");
			}
			textWriter.WriteLine();
		}
	}
}
