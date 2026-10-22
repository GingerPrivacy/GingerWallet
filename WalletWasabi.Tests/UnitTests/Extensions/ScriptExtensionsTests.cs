using NBitcoin;
using System.Linq;
using WalletWasabi.Extensions;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Extensions;

public class ScriptExtensionsTests
{
	// These encodings are part of the existing filter format, including the length prefix for witness scripts.
	[Theory]
	[InlineData("", "06")]
	[InlineData("76a914000102030405060708090a0b0c0d0e0f1011121388ac", "00000102030405060708090a0b0c0d0e0f10111213")]
	[InlineData("a914000102030405060708090a0b0c0d0e0f1011121387", "01000102030405060708090a0b0c0d0e0f10111213")]
	[InlineData("210279be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798ac", "0279be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798")]
	[InlineData("210379be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798ac", "0379be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798")]
	[InlineData("410479be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798483ada7726a3c4655da4fbfc0e1108a8fd17b448a68554199c47d08ffb10d4b8ac", "0479be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798")]
	[InlineData("410479be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798b7c52588d95c3b9aa25b0403f1eef75702e84bb7597aabe663b82f6f04ef2777ac", "0579be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798")]
	[InlineData("0014000102030405060708090a0b0c0d0e0f10111213", "1c0014000102030405060708090a0b0c0d0e0f10111213")]
	[InlineData("0020000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f", "280020000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f")]
	[InlineData("512079be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798", "28512079be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798")]
	public void CompressedScriptsPreserveFilterFormat(string scriptHex, string compressedHex)
	{
		Assert.Equal(Convert.FromHexString(compressedHex), Script.FromHex(scriptHex).ToCompressedBytes());
	}

	[Theory]
	[InlineData(121, "7f")]
	[InlineData(122, "8000")]
	[InlineData(16505, "ff7f")]
	[InlineData(16506, "808000")]
	public void ScriptLengthUsesCompactVarInt(int scriptLength, string prefixHex)
	{
		byte[] scriptBytes = Enumerable.Repeat((byte)OpcodeType.OP_RETURN, scriptLength).ToArray();
		byte[] expected = Convert.FromHexString(prefixHex).Concat(scriptBytes).ToArray();
		Assert.Equal(expected, new Script(scriptBytes).ToCompressedBytes());
	}
}
