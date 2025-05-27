using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace GingerCommon.Crypto.Random;

// The goal is to have a deterministic random generator that gives the same output for the same seed for all OS and .NET versions
// The default System.Random does not guarantee this, see https://learn.microsoft.com/en-us/dotnet/api/system.random?view=net-8.0
// This random generator is not thread safe
// Implementation: xoshiro256** https://en.wikipedia.org/wiki/Xorshift
public sealed class DeterministicRandom : GingerRandom
{
	private static object SeedGeneratorLock = new();
	private static HMACSHA256 SeedGenerator = new([59, 126, 96, 60, 244, 179, 105, 207, 41, 209, 219, 187, 5, 101, 123, 234, 168, 130, 40, 238, 199, 1, 193, 68, 190, 70, 10, 86, 237, 172, 49, 158]);

	public DeterministicRandom(ulong seed)
	{
		Span<byte> beforeHash = stackalloc byte[32];
		BinaryPrimitives.WriteUInt64LittleEndian(beforeHash, seed);
		BinaryPrimitives.WriteUInt64LittleEndian(beforeHash[8..], seed + 1);
		BinaryPrimitives.WriteUInt64LittleEndian(beforeHash[16..], seed + 2);
		BinaryPrimitives.WriteUInt64LittleEndian(beforeHash[24..], seed + 3);

		Span<byte> afterHash = stackalloc byte[32];
		lock (SeedGeneratorLock)
		{
			SeedGenerator.TryComputeHash(beforeHash, afterHash, out int res);
		}
		_s0 = BinaryPrimitives.ReadUInt64LittleEndian(afterHash);
		_s1 = BinaryPrimitives.ReadUInt64LittleEndian(afterHash[8..]);
		_s2 = BinaryPrimitives.ReadUInt64LittleEndian(afterHash[16..]);
		_s3 = BinaryPrimitives.ReadUInt64LittleEndian(afterHash[24..]);
	}

	// 32 byte long array
	public DeterministicRandom(byte[] seed)
	{
		_s0 = BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(seed, 0, 8));
		_s1 = BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(seed, 8, 8));
		_s2 = BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(seed, 16, 8));
		_s3 = BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(seed, 24, 8));
	}

	public override void GetBytes(Span<byte> buffer)
	{
		int idx = 0, sizeFull = buffer.Length, size8 = sizeFull & ~7;
		for (; idx < size8; idx += 8)
		{
			BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(idx, 8), GetUInt64());
		}
		if (idx < sizeFull)
		{
			ulong rnd = GetUInt64();
			for (; idx < sizeFull; idx++)
			{
				buffer[idx] = (byte)rnd;
				rnd >>= 8;
			}
		}
	}

	private ulong _s0;
	private ulong _s1;
	private ulong _s2;
	private ulong _s3;

	public override ulong GetUInt64()
	{
		ulong s0 = _s0, s1 = _s1, s2 = _s2, s3 = _s3;

		ulong result = BitOperations.RotateLeft(s1 * 5, 7) * 9;
		ulong t = s1 << 17;

		s2 ^= s0;
		s3 ^= s1;
		s1 ^= s2;
		s0 ^= s3;

		s2 ^= t;
		s3 = BitOperations.RotateLeft(s3, 45);

		_s0 = s0;
		_s1 = s1;
		_s2 = s2;
		_s3 = s3;

		return result;
	}
}
