using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Banning;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class CoinVerifyItemTests
{
	[Fact]
	public async Task CancellationRemainsSafeAfterDisposalAsync()
	{
		using CoinVerifyItem item = new();
		item.Dispose();
		item.Cancel();
		Assert.True(item.Token.IsCancellationRequested);
		await Task.WhenAll(Task.Run(item.Cancel), Task.Run(item.Dispose));
	}
}
