using System.IO;
using System.Text;
using System.Text.Json;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;

namespace WalletWasabi.Bases;

public static class ConfigManagerNg
{
	public static readonly JsonSerializerOptions DefaultOptions = new() { WriteIndented = true };

	public static string ToFile<T>(string filePath, T obj, JsonSerializerOptions? options = null)
	{
		options ??= DefaultOptions;

		string jsonString = JsonSerializer.Serialize(obj, options);
		File.WriteAllText(filePath, jsonString, Encoding.UTF8);
		return jsonString;
	}

	private static TResponse LoadFile<TResponse>(string filePath, JsonSerializerOptions? options = null)
	{
		options ??= DefaultOptions;

		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException($"File '{filePath}' does not exist.");
		}

		string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
		TResponse? result = JsonSerializer.Deserialize<TResponse>(jsonString, options);

		return result is not null
			? result
			: throw new JsonException("Unexpected null value.");
	}

	public static TResponse LoadFile<TResponse>(string filePath, bool createIfMissing = false, JsonSerializerOptions? options = null)
		where TResponse : IConfigNg, new()
	{
		options ??= DefaultOptions;

		TResponse result;

		if (!createIfMissing)
		{
			return LoadFile<TResponse>(filePath, options: options);
		}

		if (!File.Exists(filePath))
		{
			Logger.LogInfo($"File did not exist. Created at path: '{filePath}'.");
			result = new();
			ToFile(filePath, result, options);
		}
		else
		{
			try
			{
				return LoadFile<TResponse>(filePath, options: options);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
				string defaultFilePath = Path.ChangeExtension(filePath, "Default.json");
				try
				{
					// Never replace either the user's configuration or an existing recovery template.
					if (!File.Exists(defaultFilePath))
					{
						using var stream = File.Open(defaultFilePath, FileMode.CreateNew, FileAccess.Write);
						JsonSerializer.Serialize(stream, new TResponse(), options);
					}
				}
				catch (Exception templateException)
				{
					Logger.LogWarning(templateException);
				}

				throw new InvalidDataException($"Could not load configuration '{filePath}'. The original file has been preserved. Correct it or rename it to generate a new configuration. A default template is created at '{defaultFilePath}' when possible.", ex);
			}
		}

		return result;
	}
}
