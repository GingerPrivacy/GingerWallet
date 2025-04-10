using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GingerCommon.Static;

public static class JsonUtils
{
	public static readonly JsonSerializerOptions OptionCaseInsensitive = new() { PropertyNameCaseInsensitive = true };
}
