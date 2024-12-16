using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Models.Serialization;

namespace WalletWasabi.Bases;

public class ConfigManager
{
	private static readonly JsonSerializer Serializer = JsonSerializer.Create(JsonSerializationOptions.Default.Settings);

	public static string ToFile<T>(string filePath, T obj)
	{
		string jsonString = JsonConvert.SerializeObject(obj, Formatting.Indented, JsonSerializationOptions.Default.Settings);
		File.WriteAllText(filePath, jsonString, Encoding.UTF8);

		return jsonString;
	}

	public static bool AreDeepEqual(object current, object other)
	{
		JObject currentConfig = JObject.FromObject(current, Serializer);
		JObject otherConfigJson = JObject.FromObject(other, Serializer);
		return JToken.DeepEquals(otherConfigJson, currentConfig);
	}

	/// <summary>
	/// Check if the config file differs from the config if the file path of the config file is set, otherwise throw exception.
	/// </summary>
	public static bool CheckFileChange(string filePath, IConfig current)
	{
		object diskVersion = LoadFile(filePath, current.GetType());
		return !AreDeepEqual(diskVersion, current);
	}

	private static object LoadFile(string filePath, Type type)
	{
		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException($"File '{filePath}' does not exist.");
		}

		string jsonString = File.ReadAllText(filePath, Encoding.UTF8);

		object? result = JsonConvert.DeserializeObject(jsonString, type, JsonSerializationOptions.Default.Settings);

		return result is not null ? result : throw new JsonException("Unexpected null value.");
	}

	public static object LoadFile(string filePath, Type type, bool createIfMissing = false)
	{
		if (!createIfMissing)
		{
			return LoadFile(filePath, type);
		}

		object? result;
		if (!File.Exists(filePath))
		{
			Logger.LogInfo($"File did not exist. Created at path: '{filePath}'.");
			result = Activator.CreateInstance(type);
			ToFile(filePath, result);
		}
		else
		{
			try
			{
				return LoadFile(filePath, type);
			}
			catch (Exception ex)
			{
				result = Activator.CreateInstance(type);
				ToFile(filePath, result);

				Logger.LogInfo($"File has been deleted because it was corrupted. Recreated default version at path: '{filePath}'.");
				Logger.LogWarning(ex);
			}
		}
		if (result is null)
		{
			throw new ArgumentException($"{type} has no default constructor");
		}
		return result;
	}
}
