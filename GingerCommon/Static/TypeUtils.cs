using System;
using System.Runtime.CompilerServices;

namespace GingerCommon.Static;

public static class TypeUtils
{
	public static bool IsCompatible(this Type me, Type type)
	{
		if (me == type || me.IsSubclassOf(type))
		{
			return true;
		}
		return false;
	}

	public static T CreateAndCopy<T>(object? src = null)
	{
		var dstType = typeof(T);
		var obj = (T)RuntimeHelpers.GetUninitializedObject(dstType);

		if (src != null)
		{
			var srcType = src.GetType();
			foreach (var srcProp in srcType.GetProperties())
			{
				var value = srcProp.GetValue(src);
				var dstProp = dstType.GetProperty(srcProp.Name);
				if (dstProp != null && value != null)
				{
					dstProp.SetValue(obj, value);
				}
			}
		}
		return obj;
	}
}
