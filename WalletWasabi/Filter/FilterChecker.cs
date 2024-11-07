using NBitcoin;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Numerics;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Filter;

public class FilterChecker
{
	public static bool HasMatch(GolombRiceFilter filter, byte[] filterKey, List<byte[]> keys)
	{
		int keyCount = keys.Count;
		if (keyCount == 0)
		{
			return false;
		}

		ulong[] keyHash = new ulong[keyCount];
		UInt128 nm128 = filter.M * (ulong)filter.N;
		ulong k0 = BitConverter.ToUInt64(filterKey, 0);
		ulong k1 = BitConverter.ToUInt64(filterKey, 8);
		for (int kidx = 0; kidx < keyCount; kidx++)
		{
			ulong res = SipHash(k0, k1, keys[kidx]);
			res = (ulong)(nm128 * res >> 64);
			keyHash[kidx] = res;
		}
		Array.Sort(keyHash);

		GolombRiceReader reader = new(filter.Data, filter.P, 0);
		int num = 0;
		ulong lastHash = 0;
		while (reader.TryRead(out ulong nextKey))
		{
			for (; num < keyCount && (lastHash = keyHash[num]) < nextKey; num++) { }
			if (lastHash <= nextKey)
			{
				return lastHash == nextKey;
			}
		}

		return false;
	}

	public struct GolombRiceReader
	{
		public GolombRiceReader(byte[] data, byte p, int position)
		{
			_data = data;
			_p = p;
			_modP = 1ul << p;
			_maskP = _modP - 1;
			_position = position;
			_bitPosition = 0;
			_lastValue = 0;
		}

		public bool TryRead(out ulong value)
		{
			int count = 0;
			while (_position < _data.Length)
			{
				if (WideRead(out ulong localValue, ref count))
				{
					_lastValue += localValue;
					value = _lastValue;
					return true;
				}
			}
			value = 0;
			return false;
		}

		//[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		private bool WideRead(out ulong value, ref int count)
		{
			byte[] arr = _data;
			int pos = _position, arrLen = arr.Length, bitSize;
			ulong wide;
			if (pos + 8 <= arrLen)
			{
				wide = BinaryPrimitives.ReadUInt64BigEndian(new ReadOnlySpan<byte>(arr, pos, 8)) << _bitPosition;
				bitSize = 64 - _bitPosition;
			}
			else
			{
				bitSize = (arrLen - pos) * 8 - _bitPosition;
				if (bitSize <= _p)
				{
					_position = arrLen;
					_bitPosition = 0;
					value = 0;
					return false;
				}
				if (arrLen > 8)
				{
					pos = arrLen;
					wide = BinaryPrimitives.ReadUInt64BigEndian(new ReadOnlySpan<byte>(arr, arrLen - 8, 8));
				}
				else
				{
					wide = 0;
					for (; pos < arrLen; pos++)
					{
						wide = (wide << 8) + arr[pos];
					}
				}
				wide <<= 64 - bitSize;
			}

			int leadingOnes = BitOperations.LeadingZeroCount(~wide);
			int fullBitSize = leadingOnes + 1 + _p;
			if (fullBitSize <= bitSize)
			{
				count += leadingOnes;
				value = (wide >> 63 - leadingOnes - _p & _maskP) + (uint)count * _modP;
				fullBitSize += _bitPosition;
				_position += fullBitSize >> 3;
				_bitPosition = fullBitSize & 7;
				return true;
			}
			value = 0;
			if (pos == arrLen)
			{
				_position = pos;
				_bitPosition = 0;
				return false;
			}
			if (leadingOnes > bitSize)
			{
				leadingOnes = bitSize;
			}
			count += leadingOnes;
			int move = leadingOnes + _bitPosition;
			_position += move >> 3;
			_bitPosition = move & 7;
			return false;
		}

		private readonly byte[] _data;
		private readonly byte _p;
		private readonly ulong _modP;
		private readonly ulong _maskP;
		private int _position;
		private int _bitPosition;
		private ulong _lastValue;
	}

	public static ulong SipHash(ulong k0, ulong k1, byte[] data)
	{
		// Constructor
		ulong v0 = 0x736f6d6570736575ul ^ k0;
		ulong v1 = 0x646f72616e646f6dul ^ k1;
		ulong v2 = 0x6c7967656e657261ul ^ k0;
		ulong v3 = 0x7465646279746573ul ^ k1;

		// Write
		int size = data.Length & ~7;
		int count = 0;

		while (count < size)
		{
			ulong btmp = BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(data, count, 8));
			count += 8;

			v3 ^= btmp;

			v0 += v1;
			v2 += v3;
			v1 = v1 << 13 | v1 >> 51;
			v3 = v3 << 16 | v3 >> 48;
			v1 ^= v0;
			v3 ^= v2;
			v0 = v0 << 32 | v0 >> 32;
			v2 += v1;
			v0 += v3;
			v1 = v1 << 17 | v1 >> 47;
			v3 = v3 << 21 | v3 >> 43;
			v1 ^= v2;
			v3 ^= v0;
			v2 = v2 << 32 | v2 >> 32;

			v0 += v1;
			v2 += v3;
			v1 = v1 << 13 | v1 >> 51;
			v3 = v3 << 16 | v3 >> 48;
			v1 ^= v0;
			v3 ^= v2;
			v0 = v0 << 32 | v0 >> 32;
			v2 += v1;
			v0 += v3;
			v1 = v1 << 17 | v1 >> 47;
			v3 = v3 << 21 | v3 >> 43;
			v1 ^= v2;
			v3 ^= v0;
			v2 = v2 << 32 | v2 >> 32;

			v0 ^= btmp;
		}

		ulong ftmp;
		int dlen = data.Length;
		if (count > 0)
		{
			ftmp = BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(data, dlen - 8, 8)) >> 64 - 8 * (dlen & 7);
		}
		else
		{
			// We know that count = 0 and dlen < 8
			ftmp = 0;
			for (; count < dlen; count++)
			{
				ftmp |= (ulong)data[count] << 8 * count;
			}
		}

		// Finalize
		ftmp |= (ulong)dlen << 56;
		v3 ^= ftmp;

		v0 += v1;
		v2 += v3;
		v1 = v1 << 13 | v1 >> 51;
		v3 = v3 << 16 | v3 >> 48;
		v1 ^= v0;
		v3 ^= v2;
		v0 = v0 << 32 | v0 >> 32;
		v2 += v1;
		v0 += v3;
		v1 = v1 << 17 | v1 >> 47;
		v3 = v3 << 21 | v3 >> 43;
		v1 ^= v2;
		v3 ^= v0;
		v2 = v2 << 32 | v2 >> 32;

		v0 += v1;
		v2 += v3;
		v1 = v1 << 13 | v1 >> 51;
		v3 = v3 << 16 | v3 >> 48;
		v1 ^= v0;
		v3 ^= v2;
		v0 = v0 << 32 | v0 >> 32;
		v2 += v1;
		v0 += v3;
		v1 = v1 << 17 | v1 >> 47;
		v3 = v3 << 21 | v3 >> 43;
		v1 ^= v2;
		v3 ^= v0;
		v2 = v2 << 32 | v2 >> 32;

		v0 ^= ftmp;
		v2 ^= 0xff;

		v0 += v1;
		v2 += v3;
		v1 = v1 << 13 | v1 >> 51;
		v3 = v3 << 16 | v3 >> 48;
		v1 ^= v0;
		v3 ^= v2;
		v0 = v0 << 32 | v0 >> 32;
		v2 += v1;
		v0 += v3;
		v1 = v1 << 17 | v1 >> 47;
		v3 = v3 << 21 | v3 >> 43;
		v1 ^= v2;
		v3 ^= v0;
		v2 = v2 << 32 | v2 >> 32;

		v0 += v1;
		v2 += v3;
		v1 = v1 << 13 | v1 >> 51;
		v3 = v3 << 16 | v3 >> 48;
		v1 ^= v0;
		v3 ^= v2;
		v0 = v0 << 32 | v0 >> 32;
		v2 += v1;
		v0 += v3;
		v1 = v1 << 17 | v1 >> 47;
		v3 = v3 << 21 | v3 >> 43;
		v1 ^= v2;
		v3 ^= v0;
		v2 = v2 << 32 | v2 >> 32;

		v0 += v1;
		v2 += v3;
		v1 = v1 << 13 | v1 >> 51;
		v3 = v3 << 16 | v3 >> 48;
		v1 ^= v0;
		v3 ^= v2;
		v0 = v0 << 32 | v0 >> 32;
		v2 += v1;
		v0 += v3;
		v1 = v1 << 17 | v1 >> 47;
		v3 = v3 << 21 | v3 >> 43;
		v1 ^= v2;
		v3 ^= v0;
		v2 = v2 << 32 | v2 >> 32;

		v0 += v1;
		v2 += v3;
		v1 = v1 << 13 | v1 >> 51;
		v3 = v3 << 16 | v3 >> 48;
		v1 ^= v0;
		v3 ^= v2;
		v0 = v0 << 32 | v0 >> 32;
		v2 += v1;
		v0 += v3;
		v1 = v1 << 17 | v1 >> 47;
		v3 = v3 << 21 | v3 >> 43;
		v1 ^= v2;
		v3 ^= v0;
		v2 = v2 << 32 | v2 >> 32;

		return v0 ^ v1 ^ v2 ^ v3;
	}
}
