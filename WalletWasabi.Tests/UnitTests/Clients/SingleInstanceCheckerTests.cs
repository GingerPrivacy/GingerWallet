using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients;

/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class SingleInstanceCheckerTests
{
	/// <summary>Everything takes longer on CI. Timeouts sane on users' machines are too short for CI.</summary>
	private const int TimeoutMultiplier = 3;

	/// <summary>
	/// Global port may collide when several PRs are being tested on CI at the same time,
	/// so we need some sort of non-determinism here (i.e. random numbers).
	/// </summary>
	private static int GenerateRandomPort() => Random.Shared.Next(37128, 50000);

	[Fact]
	public async Task SingleInstanceTestsAsync()
	{
		int mainNetPort = GenerateRandomPort();
		int testNetPort = mainNetPort + 1;
		int regTestPort = testNetPort + 1;

		// Disposal test.
		await using (SingleInstanceChecker sic = new(mainNetPort, TimeoutMultiplier))
		{
			await sic.CheckSingleInstanceAsync();
		}

		// Check different networks.
		await using SingleInstanceChecker sicMainNet = new(mainNetPort, TimeoutMultiplier);
		var status = await sicMainNet.CheckSingleInstanceAsync();
		Assert.Equal(WasabiInstanceStatus.NoOtherInstanceIsRunning, status);

		await using SingleInstanceChecker sicMainNet2 = new(mainNetPort, TimeoutMultiplier);
		status = await sicMainNet.CheckSingleInstanceAsync();
		Assert.Equal(WasabiInstanceStatus.AnotherInstanceIsRunning, status);

		// testnet
		await using SingleInstanceChecker sicTestNet1 = new(testNetPort, TimeoutMultiplier);
		status = await sicTestNet1.CheckSingleInstanceAsync();
		Assert.Equal(WasabiInstanceStatus.NoOtherInstanceIsRunning, status);

		await using SingleInstanceChecker sicTestNet2 = new(testNetPort, TimeoutMultiplier);
		status = await sicTestNet2.CheckSingleInstanceAsync();
		Assert.Equal(WasabiInstanceStatus.AnotherInstanceIsRunning, status);

		// regtest
		await using SingleInstanceChecker sicRegNet1 = new(regTestPort, TimeoutMultiplier);
		status = await sicRegNet1.CheckSingleInstanceAsync();
		Assert.Equal(WasabiInstanceStatus.NoOtherInstanceIsRunning, status);

		await using SingleInstanceChecker sicRegNet2 = new(regTestPort, TimeoutMultiplier);
		status = await sicRegNet2.CheckSingleInstanceAsync();
		Assert.Equal(WasabiInstanceStatus.AnotherInstanceIsRunning, status);
	}

}
