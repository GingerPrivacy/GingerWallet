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

	public void LogException(uint256 roundId, Exception exception)
	{
		var logArray = new string[]
		{
			$"{roundId}",
			$"{exception.Message}"
		};

		AddLogLineAndFormatCsv(DateTimeOffset.UtcNow, AuditEventType.Exception, logArray);
	}

	public void LogRoundEvent(uint256 roundId, string message)
	{
		var logAsArray = new string[]
		{
			$"Round ID: {roundId}",
			$"{message}"
		};

		AddLogLineAndFormatCsv(DateTimeOffset.UtcNow, AuditEventType.Round, logAsArray);
	}

	public void LogVerificationResult(CoinVerifyResult coinVerifyResult, Reason reason, ApiResponse? apiResponse = null, Exception? exception = null)
	{
		string details = "No details";

		if (apiResponse is not null)
		{
			details = apiResponse.GetDetails();
		}
		else if (exception is not null)
		{
			details = exception.Message;
		}

		var auditAsArray = new string[]
		{
			$"{coinVerifyResult.Coin.Outpoint}",
			$"{coinVerifyResult.Coin.ScriptPubKey.GetDestinationAddress(Network.Main)}",
			$"{coinVerifyResult.ShouldBan}",
			$"{coinVerifyResult.ShouldRemove}",
			$"{coinVerifyResult.Coin.Amount}",
			$"{reason}",
			$"{details}"
		};

		AddLogLineAndFormatCsv(DateTimeOffset.UtcNow, AuditEventType.VerificationResult, auditAsArray);
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
			var auditParts = new string[]
			{
				$"{line.DateTimeOffset:yyyy-MM-dd HH:mm:ss}",
				$"{line.AuditEventType}",
				$"{line.LogMessage}"
			};

			var audit = string.Join(CsvSeparator, auditParts);
			lines.Add(audit);
		}

		var firstDate = auditLines.Select(x => x.DateTimeOffset).First();
		string filePath = Path.Combine(DirectoryPath, $"VerifierAudits.{firstDate:yyyy.MM}.txt");

		using (await FileAsyncLock.LockAsync(CancellationToken.None))
		{
			await File.AppendAllLinesAsync(filePath, lines, CancellationToken.None).ConfigureAwait(false);
		}
	}

	private void AddLogLineAndFormatCsv(DateTimeOffset dateTime, AuditEventType auditEventType, IEnumerable<string> unformattedTexts)
	{
		var csvCompatibleTexts = unformattedTexts.Select(text => text.Replace(CsvSeparator, ' '));
		var csvLine = string.Join(CsvSeparator, csvCompatibleTexts);

		lock (LogLinesLock)
		{
			LogLines.Add(new AuditEvent(dateTime, auditEventType, csvLine));
		}
	}

	public async ValueTask DisposeAsync()
	{
		await SaveAuditsAsync().ConfigureAwait(false);
	}

	public record AuditEvent(DateTimeOffset DateTimeOffset, AuditEventType AuditEventType, string LogMessage);
}
