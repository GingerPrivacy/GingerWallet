using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using WabiSabi.Crypto.Randomness;

namespace WalletWasabi.Crypto.Randomness;

// The goal is to have a deterministic random generator that gives the same output for the same seed for all OS and .NET versions
// The default System.Random does not guarantee this, see https://learn.microsoft.com/en-us/dotnet/api/system.random?view=net-8.0
// This random generator is not thread safe
// Implementation: xoshiro256** https://en.wikipedia.org/wiki/Xorshift
public class DeterministicRandom : WasabiRandom
{
	private static HMACSHA256 SeedGenerator = new([59, 126, 96, 60, 244, 179, 105, 207, 41, 209, 219, 187, 5, 101, 123, 234, 168, 130, 40, 238, 199, 1, 193, 68, 190, 70, 10, 86, 237, 172, 49, 158]);

	public DeterministicRandom(ulong seed)
	{
		byte[] beforeHash = new byte[32];
		{
			using MemoryStream inputStream = new(beforeHash, true);
			using BinaryWriter writer = new(inputStream);
			writer.Write(seed);
			writer.Write(seed + 1);
			writer.Write(seed + 2);
			writer.Write(seed + 3);
		}
		byte[] afterHash = SeedGenerator.ComputeHash(beforeHash);
		{
			using MemoryStream inputStream = new(afterHash, false);
			using BinaryReader reader = new(inputStream);
			_s0 = reader.ReadUInt64();
			_s1 = reader.ReadUInt64();
			_s2 = reader.ReadUInt64();
			_s3 = reader.ReadUInt64();
		}
	}

	public override void GetBytes(byte[] buffer)
	{
		GetBytes(new Span<byte>(buffer, 0, buffer.Length));
	}

	public override void GetBytes(Span<byte> buffer)
	{
		int idx = 0, sizeFull = buffer.Length, size8 = sizeFull & ~7;
		for (; idx < size8; idx += 8)
		{
			BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(idx, 8), Next());
		}
		if (idx < sizeFull)
		{
			ulong rnd = Next();
			for (; idx < sizeFull; idx++)
			{
				buffer[idx] = (byte)rnd;
				rnd >>= 8;
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public override int GetInt(int fromInclusive, int toExclusive) => (int)GetInt64(fromInclusive, toExclusive);

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public long GetInt64(long fromInclusive, long toExclusive)
	{
		ulong range = (ulong)(toExclusive - fromInclusive);

		// think of Next() as a [0,1) number, multiplying it means, the upper part will be [0,range)
		ulong high = Math.BigMul(range, Next(), out ulong low);

		if (low < range)
		{
			// we might be at the range of extra probability of randomProduct
			ulong remainder = (0ul - range) % range;
			while (low < remainder)
			{
				// this reminder is an "extra" element for the randomProduct value, other values don't have it, so just get a new one
				// to better understand this, try it with 4bit "ulong" (0..7) and range 3 => 0 gives extra results compared to others
				// try a new one
				high = Math.BigMul(range, Next(), out low);
			}
		}

		return fromInclusive + (long)high;
	}

	private ulong _s0;
	private ulong _s1;
	private ulong _s2;
	private ulong _s3;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ulong Next()
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
