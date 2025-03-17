using System;
using System.IO;
using Microsoft.Extensions.Logging;
using GingerCommon.Static;
using static GingerCommon.Logging.Logger;
using System.Collections.Generic;

namespace GingerCommon.Logging;

public class FileLogger : ILogger, IDisposable
{
	private readonly string _categoryName;
	private StreamWriter? _logFileWriter;
	private long _logFileSizeBytes;
	private string _fileName;
	private object _lock = new object();

	public long MaximumLogFileSizeBytes { get; set; }

	public FileLogger(string categoryName, string fileName)
	{
		_categoryName = categoryName;
		_fileName = fileName;
		_logFileSizeBytes = 0;
		MaximumLogFileSizeBytes = 0;
		OpenFileLockless();
	}

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull
	{
		return NullScope.Instance;
	}

	public bool IsEnabled(LogLevel logLevel)
	{
		return _logFileWriter is not null && logLevel != LogLevel.None;
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (!IsEnabled(logLevel) || _logFileWriter is null)
		{
			return;
		}

		var message = formatter(state, exception);

		lock (_lock)
		{
			long maximumLogFileSizeBytes = MaximumLogFileSizeBytes;
			if (_logFileSizeBytes > maximumLogFileSizeBytes && maximumLogFileSizeBytes > 0)
			{
				CloseFileLockless();
				File.Delete(_fileName);
				OpenFileLockless();
			}
			_logFileWriter.WriteLine(message);
			_logFileSizeBytes += message.Length + 1;
		}
	}

	public void CloseFile()
	{
		lock (_lock)
		{
			if (_logFileWriter is not null)
			{
				CloseFileLockless();
			}
		}
	}

	public void Dispose()
	{
		CloseFileLockless();
	}

	private void OpenFileLockless()
	{
		IOHelpers.EnsureFileExists(_fileName);
		FileStreamOptions options = new()
		{
			Mode = FileMode.Append,
			Share = FileShare.ReadWrite,
			Access = FileAccess.Write,
		};
		_logFileWriter = new StreamWriter(_fileName, options);
		_logFileWriter.AutoFlush = true;
		_logFileSizeBytes = new FileInfo(_fileName).Length;
	}

	private void CloseFileLockless()
	{
		_logFileWriter?.Close();
		_logFileWriter = null;
	}
}

[ProviderAlias("File")]
public class FileLoggerProvider : ILoggerProvider
{
	private long _maximumLogFileSizeBytes;
	private string _fileName;

	private static object LoggersLock = new object();
	private static Dictionary<string, FileLogger> Loggers = new();

	public FileLoggerProvider(string fileName, long maximumLogFileSizeBytes = 10_000_000)
	{
		_fileName = fileName;
		_maximumLogFileSizeBytes = maximumLogFileSizeBytes;
	}

	public ILogger CreateLogger(string categoryName)
	{
		// There is no good way to remove an existing ILogger
		// We need to keep track and reuse them instead or we would get an exception at the second open attempt
		lock (LoggersLock)
		{
			if (!Loggers.TryGetValue(_fileName, out FileLogger? logger))
			{
				logger = new FileLogger(categoryName, _fileName);
				Loggers[_fileName] = logger;
			}
			logger.MaximumLogFileSizeBytes = _maximumLogFileSizeBytes;
			return logger;
		}
	}

	public void Dispose()
	{
	}

	public static void CloseFiles()
	{
		lock (LoggersLock)
		{
			foreach (var logger in Loggers.Values)
			{
				logger.CloseFile();
			}
		}
	}
}
