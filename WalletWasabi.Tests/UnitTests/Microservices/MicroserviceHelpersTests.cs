using System.IO;
using System.Runtime.InteropServices;
using WalletWasabi.Microservices;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Microservices;

public class MicroserviceHelpersTests
{
	[Theory]
	[InlineData(Architecture.X64, "linux-x64")]
	[InlineData(Architecture.Arm64, "linux-arm64")]
	public void SelectsLinuxBinaryFolderForProcessArchitecture(Architecture architecture, string expectedFolder)
	{
		string binaryFolder = MicroserviceHelpers.GetBinaryFolder(OSPlatform.Linux, architecture);

		Assert.Equal(expectedFolder, Path.GetFileName(binaryFolder));
	}
}
