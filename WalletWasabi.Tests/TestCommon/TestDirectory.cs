using GingerCommon.Logging;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Tests.TestCommon;

public static class TestDirectory
{
	static TestDirectory()
	{
		Logger.CreateLogger(LogLevel.None, LogLevel.Information, LogLevel.Information, Path.Combine(DataDir, "Logs.txt"));
	}

	public static readonly string DataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Tests"));

	// Comes back with an empty directory for the first time during the process
	public static string Get([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
	{
		string key = $"{callerFilePath}+{callerMemberName}";
		string pathStart = $"{Path.Combine(DataDir, EnvironmentHelpers.ExtractFileName(callerFilePath), callerMemberName)}";

		TestDirectoryCleanup(pathStart);

		string path = "";
		Exception? exception = null;
		lock (WorkingDirectories)
		{
			if (WorkingDirectories.TryGetValue(key, out var workingDirectory))
			{
				return workingDirectory.Path;
			}

			int processId = Environment.ProcessId;
			for (int subId = 0; subId < 100; subId++)
			{
				try
				{
					path = $"{pathStart}_{processId}_{subId:D2}";
					if (!Directory.Exists(path))
					{
						Directory.CreateDirectory(path);
#pragma warning disable CA2000 // Dispose objects before losing scope
						var stream = File.Open(Path.Combine(path, "lock.txt"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
#pragma warning restore CA2000
						stream.Write(Encoding.UTF8.GetBytes($"Created at {DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}\n"));
						stream.Flush();
						WorkingDirectories[key] = new(path, stream);
						return path;
					}
				}
				catch (Exception ex)
				{
					exception = ex;
				}
			}
		}
		throw exception ?? new InvalidOperationException();
	}

	private static string[] LockFiles = ["regtest/debug.log", "lock.txt"];

	private static void TestDirectoryCleanup(string path)
	{
		try
		{
			var root = Path.GetDirectoryName(path);
			if (root is not null)
			{
				foreach (var dir in Directory.EnumerateDirectories(root))
				{
					try
					{
						if (dir.StartsWith(path))
						{
							foreach (var lockFileName in LockFiles)
							{
								var lockFile = Path.Combine(dir, lockFileName);
								if (File.Exists(lockFile))
								{
									{
										using var stream = File.Open(Path.Combine(path, "lock.txt"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
										stream.Write(Encoding.UTF8.GetBytes($"Deleted at {DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}\n"));
									}
									File.Delete(lockFile);
								}
							}
							Directory.Delete(dir, recursive: true);
						}
					}
					catch { }
				}
			}
		}
		catch { }
	}

	// We never close the stream, used as a lock
	private record WorkingDirectory(string Path, FileStream? LockStream);

	private static Dictionary<string, WorkingDirectory> WorkingDirectories = new();
}
