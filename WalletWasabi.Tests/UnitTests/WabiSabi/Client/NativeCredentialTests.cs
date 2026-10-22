using Newtonsoft.Json;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using WabiSabi.Crypto;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.Serialization;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class NativeCredentialTests
{
	[Theory]
	[InlineData("win-x64", 10, true)]
	[InlineData("win-arm64", 10, false)]
	[InlineData("linux-x64", 6, true)]
	[InlineData("linux-arm64", 6, true)]
	[InlineData("linux-musl-x64", 6, false)]
	[InlineData("linux-arm", 6, false)]
	[InlineData("osx-x64", 15, false)]
	[InlineData("osx-arm64", 15, false)]
	[InlineData("osx-x64", 25, false)]
	[InlineData("osx-arm64", 25, false)]
	[InlineData("osx-x64", 26, true)]
	[InlineData("osx-arm64", 26, true)]
	[InlineData("osx-arm64", 27, true)]
	public void NativeSelectionRespectsPackagedPlatforms(string runtimeIdentifier, int osMajorVersion, bool expected)
	{
		Assert.Equal(expected, CredentialClient.IsNativeSupported(runtimeIdentifier, new Version(osMajorVersion, 0)));
	}

	[Theory]
	[InlineData(100_000_000L, 200_000_000L, false)]
	[InlineData(2_000_000_000_000L, 2_100_000_000_000L, false)]
	[InlineData(100_000_000L, 200_000_000L, true)]
	[InlineData(2_000_000_000_000L, 2_100_000_000_000L, true)]
	public void CredentialClientsCompleteProtocolWithManagedIssuers(long firstAmount, long secondAmount, bool forceManaged)
	{
		var round = WabiSabiTestFactory.CreateRound(WabiSabiTestFactory.CreateDefaultWabiSabiConfig());
		var roundState = RoundState.FromRound(round);
		var amountClient = forceManaged
			? new CredentialClient(round.AmountCredentialIssuerParameters, TestRandom.Wasabi(1), round.AmountCredentialIssuer.MaxAmount, useNative: false)
			: roundState.CreateAmountCredentialClient(TestRandom.Wasabi(1));
		var vsizeClient = forceManaged
			? new CredentialClient(round.VsizeCredentialIssuerParameters, TestRandom.Wasabi(2), round.VsizeCredentialIssuer.MaxAmount, useNative: false)
			: roundState.CreateVsizeCredentialClient(TestRandom.Wasabi(2));
		var expectNative = !forceManaged && CredentialClient.IsNativeSupported(RuntimeInformation.RuntimeIdentifier, Environment.OSVersion.Version);
		Assert.Equal(expectNative, amountClient.IsNative);
		Assert.Equal(expectNative, vsizeClient.IsNative);
		Assert.Equal(42, amountClient.RangeProofWidth);

		CompleteProtocol(amountClient, round.AmountCredentialIssuer, [firstAmount, secondAmount]);
		CompleteProtocol(vsizeClient, round.VsizeCredentialIssuer, [31L, 31L]);
	}

	private static void CompleteProtocol(CredentialClient client, CredentialIssuer issuer, long[] amounts)
	{
		var zeroRequest = client.CreateRequestForZeroAmount();
		var zeroResponse = issuer.HandleRequest(RoundTrip(zeroRequest.CredentialsRequest));
		var zeroCredentials = client.HandleResponse(RoundTrip(zeroResponse), zeroRequest.CredentialsResponseValidation).ToArray();
		Assert.Equal(client.NumberOfCredentials, zeroCredentials.Length);
		Assert.All(zeroCredentials, credential => Assert.Equal(0L, credential.Value));

		var realRequest = client.CreateRequest(amounts, zeroCredentials, CancellationToken.None);
		var realResponse = issuer.HandleRequest(RoundTrip(realRequest.CredentialsRequest));
		var realCredentials = client.HandleResponse(RoundTrip(realResponse), realRequest.CredentialsResponseValidation).ToArray();
		Assert.Equal(amounts, realCredentials.Select(credential => credential.Value));

		var reissuedAmounts = new[] { amounts.Sum() - 1, 1L };
		var reissuance = client.CreateRequest(reissuedAmounts, realCredentials, CancellationToken.None);
		Assert.Equal(0L, reissuance.CredentialsRequest.Delta);
		var reissuanceResponse = issuer.HandleRequest(RoundTrip(reissuance.CredentialsRequest));
		var reissuedCredentials = client.HandleResponse(RoundTrip(reissuanceResponse), reissuance.CredentialsResponseValidation).ToArray();
		Assert.Equal(reissuedAmounts, reissuedCredentials.Select(credential => credential.Value));

		var presentation = client.CreateRequest(reissuedCredentials, CancellationToken.None);
		Assert.Empty(presentation.CredentialsRequest.Requested);
		Assert.Equal(-amounts.Sum(), presentation.CredentialsRequest.Delta);
		var presentationResponse = issuer.HandleRequest(RoundTrip(presentation.CredentialsRequest));
		Assert.Empty(presentationResponse.IssuedCredentials);
		Assert.Empty(presentationResponse.Proofs);
		Assert.Equal(0L, issuer.Balance);
	}

	private static T RoundTrip<T>(T value)
	{
		var serialized = JsonConvert.SerializeObject(value, JsonSerializationOptions.Default.Settings);
		var deserialized = JsonConvert.DeserializeObject<T>(serialized, JsonSerializationOptions.Default.Settings);
		Assert.NotNull(deserialized);
		Assert.Equal(serialized, JsonConvert.SerializeObject(deserialized, JsonSerializationOptions.Default.Settings));
		return deserialized;
	}
}
