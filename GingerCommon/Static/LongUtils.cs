using System;
using System.Numerics;

namespace GingerCommon.Static;

public static class LongUtils
{
	public static int BitWidth(long value)
	{
		if (value >= 0)
		{
			return 64 - BitOperations.LeadingZeroCount((ulong)value);
		}
		throw new ArgumentOutOfRangeException("value");
	}

	// value > 0
	public static bool IsPowerOf2(long value)
	{
		return (value & (value - 1)) == 0;
	}

	// Gives back the last reminder (x) or 0 if value is not in the format of x * power^y
	public static bool HasMultiplierAndExponent(long value, long power, out long multiplier, out int exponent)
	{
		long result = value, reminder = 0;
		int exp = 0;
		while (result >= power && reminder == 0)
		{
			result = Math.DivRem(result, power, out reminder);
			exp++;
		}
		if (reminder != 0)
		{
			multiplier = exponent = 0;
			return false;
		}
		multiplier = result;
		exponent = exp;
		return true;
	}
}
