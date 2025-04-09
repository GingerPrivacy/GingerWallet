using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static GingerCommon.Logging.Logger;

namespace GingerCommon.Logging;

public class DiscordLogger : ILogger
{
	private readonly string _categoryName;
	private string _webhook;
	private HttpClient _httpClient;

	public long MaximumLogFileSizeBytes { get; set; }

	public DiscordLogger(string categoryName, HttpClient httpClient, string webhook)
	{
		_categoryName = categoryName;
		_webhook = webhook;
		_httpClient = httpClient;
	}

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull
	{
		return NullScope.Instance;
	}

	public bool IsEnabled(LogLevel logLevel)
	{
		return !string.IsNullOrEmpty(_webhook) && logLevel != LogLevel.None;
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (!IsEnabled(logLevel))
		{
			return;
		}

		var message = formatter(state, exception);

		if (string.IsNullOrEmpty(message))
		{
			return;
		}

		Task.Run(async () =>
		{
			try
			{
				var content = new { content = message };
				var json = JsonSerializer.Serialize(content);
				using var data = new StringContent(json, Encoding.UTF8, "application/json");
				HttpResponseMessage response = await _httpClient.PostAsync(_webhook, data);
				string result = await response.Content.ReadAsStringAsync();
			}
			catch (Exception ex)
			{
				Logger.Log("Error during sending log to Discord", ex, LogLevel.Warning);
			}
		});
	}
}

[ProviderAlias("Discord")]
public class DiscordLoggerProvider : ILoggerProvider
{
	private string _webhook;
	private HttpClient _httpClient;

	private static object LoggersLock = new object();
	private static Dictionary<string, DiscordLogger> Loggers = new();

	public DiscordLoggerProvider(IHttpClientFactory httpClientFactory, string webhook)
	{
		_httpClient = httpClientFactory.CreateClient("Discord");
		_webhook = webhook;
	}

	public ILogger CreateLogger(string categoryName)
	{
		lock (LoggersLock)
		{
			if (!Loggers.TryGetValue(_webhook, out DiscordLogger? logger))
			{
				logger = new DiscordLogger(categoryName, _httpClient, _webhook);
				Loggers[_webhook] = logger;
			}
			return logger;
		}
	}

	public void Dispose()
	{
	}
}
