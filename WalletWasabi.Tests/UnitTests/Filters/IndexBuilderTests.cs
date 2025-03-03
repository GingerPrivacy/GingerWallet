using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Filter;
using WalletWasabi.Tests.TestCommon;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Filters;

public class IndexBuilderTests
{
	[Fact]
	public void DummyFilterMatchesToFalse()
	{
		var rnd = TestRandom.Get(123456);
		var blockHash = new byte[32];
		rnd.GetBytes(blockHash);

		var filter = IndexBuilderService.CreateDummyEmptyFilter(new uint256(blockHash));

		var scriptPubKeys = Enumerable.Range(0, 1000).Select(x =>
		{
			var buffer = new byte[20];
			rnd.GetBytes(buffer);
			return buffer;
		}).ToList();
		var key = blockHash[0..16];
		Assert.False(filter.MatchAny(scriptPubKeys, key));
		Assert.True(filter.MatchAny(IndexBuilderService.DummyScript, key));

		Assert.False(FilterChecker.HasMatch(filter, key, scriptPubKeys));
		Assert.True(FilterChecker.HasMatch(filter, key, IndexBuilderService.DummyScript.ToList()));
	}
}
