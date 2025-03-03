using System;
using System.Security.Cryptography;

namespace GingerCommon.Crypto.Random;

public class SecureRandom : GingerRandom
{
	public static readonly SecureRandom Instance = new();

	public override void GetBytes(Span<byte> buffer)
	{
		RandomNumberGenerator.Fill(buffer);
	}
}
