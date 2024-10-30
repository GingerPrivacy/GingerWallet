using System.Collections.Generic;
using System.Numerics;

namespace WalletWasabi.Extensions;

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
}
