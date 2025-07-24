using System.IO;
using System.Net;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.Tor;

namespace WalletWasabi.Tests.Helpers;

public static class Common
{
	public static EndPoint TorSocks5Endpoint => new IPEndPoint(IPAddress.Loopback, TorSettings.DefaultSocksPort);
	public static string TorDistributionFolder => Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "TorDaemons");

	/// <remarks>Tor is instructed to terminate on exit because this Tor instance would prevent running your Wasabi Wallet where Tor is started with data in a different folder.</remarks>
	public static TorSettings TorSettings => new(TestDirectory.DataDir, TorDistributionFolder, terminateOnExit: true);
}
