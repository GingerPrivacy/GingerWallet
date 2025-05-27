using NBitcoin;
using System;

namespace GingerCommon.Static;

public static class NBitcoinUtils
{
	private static readonly Network[] Networks = [Network.Main, Network.RegTest, Network.TestNet];

	public static ExtPubKey ParseExtPubKey(string extPubKeyString)
	{
		extPubKeyString = extPubKeyString.SafeTrim();

		foreach (var network in Networks)
		{
			try
			{
				return ExtPubKey.Parse(extPubKeyString, network);
			}
			catch { }
		}
		return new ExtPubKey(Convert.FromHexString(extPubKeyString));
	}
}
