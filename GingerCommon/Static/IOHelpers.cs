using System.IO;

namespace GingerCommon.Static;

public static class IOHelpers
{
	public static void EnsureContainingDirectoryExists(string fileNameOrPath)
	{
		string fullPath = Path.GetFullPath(fileNameOrPath); // No matter if relative or absolute path is given to this.
		string? dir = Path.GetDirectoryName(fullPath);
		EnsureDirectoryExists(dir);
	}

	public static void EnsureDirectoryExists(string? dir)
	{
		if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
		{
			Directory.CreateDirectory(dir);
		}
	}

	public static void EnsureFileExists(string filePath)
	{
		if (!File.Exists(filePath))
		{
			EnsureContainingDirectoryExists(filePath);
			File.Create(filePath)?.Dispose();
		}
	}
}
