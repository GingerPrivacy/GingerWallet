using System.Text.Json;

namespace GingerCommon.Static;

public static class JsonUtils
{
	public static readonly JsonSerializerOptions OptionCaseInsensitive = new() { PropertyNameCaseInsensitive = true };
}
