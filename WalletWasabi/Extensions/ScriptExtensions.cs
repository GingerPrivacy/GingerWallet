// Adapted from NBitcoin's ScriptCompressor:
// https://github.com/MetacoSA/NBitcoin/blob/7517f75c6f3bc203d0b7f13a68112dd3e1086d88/NBitcoin/Transaction.cs
//
// The MIT License (MIT)
//
// Copyright (c) 2023 Metaco SA
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using NBitcoin;
using System.IO;

namespace WalletWasabi.Extensions;

public static class ScriptExtensions
{
	/// <summary>
	/// Preserves the NBitcoin 9 compressed-script encoding used by Ginger's block filters.
	/// </summary>
	public static byte[] ToCompressedBytes(this Script script)
	{
		if (PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(script) is { } keyId)
		{
			byte[] result = new byte[21];
			keyId.ToBytes().CopyTo(result, 1);
			return result;
		}

		if (PayToScriptHashTemplate.Instance.ExtractScriptPubKeyParameters(script) is { } scriptId)
		{
			byte[] result = new byte[21];
			result[0] = 0x01;
			scriptId.ToBytes().CopyTo(result, 1);
			return result;
		}

		if (PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(script, true) is { } pubKey)
		{
			byte[] pubKeyBytes = pubKey.ToBytes();
			if (pubKeyBytes[0] is 0x02 or 0x03 or 0x04)
			{
				byte[] result = new byte[33];
				pubKeyBytes.AsSpan(1, 32).CopyTo(result.AsSpan(1));
				result[0] = pubKeyBytes[0] == 0x04
					? (byte)(0x04 | (pubKeyBytes[64] & 0x01))
					: pubKeyBytes[0];
				return result;
			}
		}

		using MemoryStream stream = new();
		BitcoinStream bitcoinStream = new(stream, serializing: true);
		uint size = (uint)script.Length + 6;
		bitcoinStream.ReadWriteAsCompactVarInt(ref size);
		bitcoinStream.ReadWrite(script.ToBytes());
		return stream.ToArray();
	}
}
