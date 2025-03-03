using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace GingerCommon.Crypto.Random;

public abstract class GingerRandom
{
	public abstract void GetBytes(Span<byte> output);

	public virtual ulong GetUInt64()
	{
		Span<byte> bytes = stackalloc byte[8];
		BinaryPrimitives.WriteUInt64LittleEndian(bytes, 0);
		GetBytes(bytes);
		ulong res = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
		return res;
	}

	// Simple solution
	public virtual int GetInt(int fromInclusive, int toExclusive) => (int)GetInt64(fromInclusive, toExclusive);

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public virtual long GetInt64(long fromInclusive, long toExclusive)
	{
		ulong range = (ulong)(toExclusive - fromInclusive);

		// think of Next() as a [0,1) number, multiplying it means, the upper part will be [0,range)
		ulong high = Math.BigMul(range, GetUInt64(), out ulong low);

		if (low < range)
		{
			// we might be at the range of extra probability of randomProduct
			ulong remainder = (0ul - range) % range;
			while (low < remainder)
			{
				// this reminder is an "extra" element for the randomProduct value, other values don't have it, so just get a new one
				// to better understand this, try it with 4bit "ulong" (0..7) and range 3 => 0 gives extra results compared to others
				// try a new one
				high = Math.BigMul(range, GetUInt64(), out low);
			}
		}

		return fromInclusive + (long)high;
	}
}
