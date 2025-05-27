using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifierLogger : IAsyncDisposable
{
	private const char CsvSeparator = ',';

	public CoinVerifierLogger(string directoryPath)
	{
		IoHelpers.EnsureDirectoryExists(directoryPath);
		DirectoryPath = directoryPath;
	}

	private string DirectoryPath { get; }

	private AsyncLock FileAsyncLock { get; } = new();

	private object LogLinesLock { get; } = new();

	private List<AuditEvent> LogLines { get; } = new();

	public void LogException(uint256 roundId, string exceptionLevel, Exception exception)
	{
		var logArray = new string[]
		{
			exceptionLevel,
			$"{roundId}",
			$"{exception.Message}"
		};

		AddLogLineAndFormatCsv(DateTimeOffset.UtcNow, logArray);
	}

	public void LogVerificationResult(CoinVerifyResult coinVerifyResult, AuditResultType resultType, ApiResponse? apiResponse = null, Exception? exception = null)
	{
		string resultTypeStr = $"{resultType}";
		string details = "";

		if (apiResponse is not null)
		{
			if (apiResponse.Provider.Length > 0)
			{
				resultTypeStr = apiResponse.Provider[0..1].ToUpperInvariant() + apiResponse.Provider[1..].ToLowerInvariant();
			}
			details = apiResponse.Details;
		}
		else if (exception is not null)
		{
			details = exception.Message;
		}

		var auditAsArray = new string[]
		{
			$"{coinVerifyResult.Coin.Outpoint}",
			$"{coinVerifyResult.Coin.ScriptPubKey.GetDestinationAddress(Network.Main)}",
			$"{coinVerifyResult.Coin.Amount}",
			coinVerifyResult.ShouldBan ? "Banned" : (coinVerifyResult.ShouldRemove ? "Removed" : "Allowed"),
			$"{resultTypeStr}",
			$"{details}"
		};

		AddLogLineAndFormatCsv(DateTimeOffset.UtcNow, auditAsArray);
	}

	public async Task SaveAuditsAsync()
	{
		AuditEvent[] auditLines;

		lock (LogLinesLock)
		{
			auditLines = LogLines.ToArray();
			LogLines.Clear();
		}

		if (auditLines.Length == 0)
		{
			return;
		}

		List<string> lines = new();
		foreach (AuditEvent line in auditLines)
		{
			lines.Add($"{line.DateTimeOffset:yyyy-MM-dd HH:mm:ss}{CsvSeparator}{line.LogMessage}");
		}

		var firstDate = auditLines.Select(x => x.DateTimeOffset).First();
		string filePath = Path.Combine(DirectoryPath, $"VerifierAudits.{firstDate:yyyy.MM}.txt");

		using (await FileAsyncLock.LockAsync(CancellationToken.None))
		{
			await File.AppendAllLinesAsync(filePath, lines, CancellationToken.None).ConfigureAwait(false);
		}
	}

	private void AddLogLineAndFormatCsv(DateTimeOffset dateTime, IEnumerable<string> unformattedTexts)
	{
		var csvCompatibleTexts = unformattedTexts.Select(text => text.Replace(CsvSeparator, ' '));
		var csvLine = string.Join(CsvSeparator, csvCompatibleTexts);

		lock (LogLinesLock)
		{
			LogLines.Add(new AuditEvent(dateTime, csvLine));
		}
	}

	public async ValueTask DisposeAsync()
	{
		await SaveAuditsAsync().ConfigureAwait(false);
	}

	public record AuditEvent(DateTimeOffset DateTimeOffset, string LogMessage);
}
