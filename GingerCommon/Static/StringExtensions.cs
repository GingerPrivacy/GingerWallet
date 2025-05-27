using System;

namespace GingerCommon.Static;

public static class StringExtensions
{
	/// <summary>
	/// Safe method means that the "this" object can be null and handled properly.
	/// </summary>

	public static string SafeTrim(this string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
	}

	/// <summary>
	/// Removes one leading occurrence of the specified string
	/// </summary>
	public static string TrimStart(this string me, string trimString, StringComparison comparisonType)
	{
		if (me.StartsWith(trimString, comparisonType))
		{
			return me[trimString.Length..];
		}
		return me;
	}

	/// <summary>
	/// Removes one trailing occurrence of the specified string
	/// </summary>
	public static string TrimEnd(this string me, string trimString, StringComparison comparisonType)
	{
		if (me.EndsWith(trimString, comparisonType))
		{
			return me[..^trimString.Length];
		}
		return me;
	}

	/// <summary>
	/// New lines should end with '\n'.
	/// </summary>
	public static string StandardLineEndings(this string me)
	{
		return me.ReplaceLineEndings("\n");
	}

	// This method removes the path and file extension.
	//
	// Given Ginger releases are currently built using Windows, the generated assemblies contain
	// the hard coded "C:\Users\User\Desktop\...\FileName.cs" string because that
	// is the real path of the file, it doesn't matter what OS was targeted.
	// In Windows and Linux that string is a valid path and that means Path.GetFileNameWithoutExtension
	// can extract the file name but in the case of OSX the same string is not a valid path so, it assumes
	// the whole string is the file name.
	public static string ExtractFileName(this string callerFilePath)
	{
		var lastSeparatorIndex = callerFilePath.LastIndexOf('\\');
		if (lastSeparatorIndex == -1)
		{
			lastSeparatorIndex = callerFilePath.LastIndexOf('/');
		}

		var fileName = callerFilePath[++lastSeparatorIndex..]; // From lastSeparatorIndex until the end of the string.

		var fileNameWithoutExtension = fileName.TrimEnd(".cs", StringComparison.InvariantCultureIgnoreCase);
		return fileNameWithoutExtension;
	}
}
