using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Randomness;
using WabiSabi.Crypto.ZeroKnowledge;
using ManagedClient = WabiSabi.Crypto.WabiSabiClient;
using NativeClient = WabiSabi.Native.WabiSabiClient;

namespace WalletWasabi.WabiSabi.Client;

public sealed class CredentialClient
{
	private readonly ManagedClient? _managed;
	private readonly NativeClient? _native;

	public CredentialClient(CredentialIssuerParameters issuerParameters, WasabiRandom random, long rangeProofUpperBound)
		: this(issuerParameters, random, rangeProofUpperBound, IsNativeSupported(RuntimeInformation.RuntimeIdentifier, Environment.OSVersion.Version))
	{
	}

	internal CredentialClient(CredentialIssuerParameters issuerParameters, WasabiRandom random, long rangeProofUpperBound, bool useNative)
	{
		if (useNative)
		{
			_native = new(issuerParameters, random, rangeProofUpperBound);
		}
		else
		{
			_managed = new(issuerParameters, random, rangeProofUpperBound);
		}
	}

	public int RangeProofWidth => _native?.RangeProofWidth ?? _managed!.RangeProofWidth;
	public int NumberOfCredentials => _native?.NumberOfCredentials ?? _managed!.NumberOfCredentials;
	internal bool IsNative => _native is not null;

	internal static bool IsNativeSupported(string runtimeIdentifier, Version osVersion) => runtimeIdentifier switch
	{
		"win-x64" or "linux-x64" or "linux-arm64" => true,
		// The WabiSabi 1.4.3 dylibs declare macOS 26.0 as their minimum deployment target.
		"osx-x64" or "osx-arm64" => osVersion.Major >= 26,
		_ => false
	};

	public ZeroCredentialsRequestData CreateRequestForZeroAmount() =>
		_native is { } native ? native.CreateRequestForZeroAmount() : _managed!.CreateRequestForZeroAmount();

	public RealCredentialsRequestData CreateRequest(IEnumerable<Credential> credentialsToPresent, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return _native is { } native
			? native.CreateRequest(credentialsToPresent, cancellationToken)
			: _managed!.CreateRequest(credentialsToPresent, cancellationToken);
	}

	public RealCredentialsRequestData CreateRequest(IEnumerable<long> amountsToRequest, IEnumerable<Credential> credentialsToPresent, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return _native is { } native
			? native.CreateRequest(amountsToRequest, credentialsToPresent, cancellationToken)
			: _managed!.CreateRequest(amountsToRequest, credentialsToPresent, cancellationToken);
	}

	public IEnumerable<Credential> HandleResponse(CredentialsResponse response, CredentialsResponseValidation validation) =>
		_native is { } native ? native.HandleResponse(response, validation) : _managed!.HandleResponse(response, validation);
}
