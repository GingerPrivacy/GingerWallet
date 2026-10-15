using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WalletWasabi.TrezorSuite;

public sealed class TrezorSuiteLocator
{
	public string? Locate(string? preferredPath = null, string? previouslyUsedPath = null)
	{
		return EnumerateCandidates(preferredPath, previouslyUsedPath)
			.FirstOrDefault(File.Exists);
	}

	public IEnumerable<string> EnumerateCandidates(string? preferredPath = null, string? previouslyUsedPath = null)
	{
		var candidates = new List<string>();

		AddCandidate(candidates, preferredPath);
		AddCandidate(candidates, previouslyUsedPath);

		if (OperatingSystem.IsWindows())
		{
			AddWindowsCandidates(candidates);
		}
		else if (OperatingSystem.IsMacOS())
		{
			AddCandidate(candidates, "/Applications/Trezor Suite.app/Contents/MacOS/Trezor Suite");
			AddCandidate(candidates, Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				"Applications",
				"Trezor Suite.app",
				"Contents",
				"MacOS",
				"Trezor Suite"));
		}
		else if (OperatingSystem.IsLinux())
		{
			AddCandidate(candidates, "/usr/bin/trezor-suite");
			AddCandidate(candidates, "/usr/local/bin/trezor-suite");
			AddCandidate(candidates, "/opt/Trezor Suite/trezor-suite");
			AddCandidate(candidates, "/opt/trezor-suite/trezor-suite");
			AddPathCandidates(candidates, "trezor-suite", "Trezor-Suite");
		}

		return candidates.Distinct(PathComparer);
	}

	internal static StringComparer PathComparer => OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

	private static void AddWindowsCandidates(ICollection<string> candidates)
	{
		var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
		var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

		AddCandidate(candidates, Path.Combine(localAppData, "Programs", "Trezor Suite", "Trezor Suite.exe"));
		AddCandidate(candidates, Path.Combine(programFiles, "Trezor Suite", "Trezor Suite.exe"));
		AddCandidate(candidates, Path.Combine(programFilesX86, "Trezor Suite", "Trezor Suite.exe"));
		AddPathCandidates(candidates, "Trezor Suite.exe");
	}

	private static void AddPathCandidates(ICollection<string> candidates, params string[] executableNames)
	{
		var path = Environment.GetEnvironmentVariable("PATH");
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}

		foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			foreach (var executableName in executableNames)
			{
				AddCandidate(candidates, Path.Combine(directory, executableName));
			}
		}
	}

	private static void AddCandidate(ICollection<string> candidates, string? candidate)
	{
		if (!string.IsNullOrWhiteSpace(candidate))
		{
			candidates.Add(Path.GetFullPath(candidate));
		}
	}
}
