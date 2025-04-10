using System;
using System.Collections.Generic;
using System.Numerics;

namespace GingerCommon.Static;

public static class DictionaryExtensions
{
	public static void AddValue<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value) where TValue : INumber<TValue>
	{
		if (!dict.TryAdd(key, value))
		{
			dict[key] += value;
		}
	}

	public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key) where TValue : new()
	{
		if (!dict.TryGetValue(key, out TValue? value))
		{
			dict.Add(key, value = new());
		}
		return value;
	}

	public static Dictionary<TKey, TValueNew> SafeConvert<TKey, TValue, TValueNew>(this IDictionary<TKey, TValue>? dict, Func<TValue, (bool, TValueNew)> func) where TKey : notnull
	{
		Dictionary<TKey, TValueNew> result = new();
		if (dict is not null)
		{
			foreach (var element in dict)
			{
				var (add, value) = func.Invoke(element.Value);
				if (add)
				{
					result[element.Key] = value;
				}
			}
		}
		return result;
	}
}
