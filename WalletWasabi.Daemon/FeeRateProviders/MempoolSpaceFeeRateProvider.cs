using NBitcoin;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Tor.Http;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Daemon.FeeRateProviders;

/// <summary>
/// A fee rate provider that uses the mempool.space REST API to retrieve recommended fee rates.
/// </summary>
public class MempoolSpaceFeeRateProvider : IFeeRateProvider
{
	private const string ApiUrl = "https://mempool.space/api/v1/";
	private const string TestNetApiUrl = "https://mempool.space/testnet/api/v1/";

	// Define the mappings between JSON property names and target blocks
	private static readonly (string JsonKey, int TargetBlocks)[] FeeMappings =
	{
		("fastestFee", 2),
		("halfHourFee", 3),
		("hourFee", 6),
		("economyFee", 72)
	};

	private IHttpClient HttpClient { get; }

	public MempoolSpaceFeeRateProvider(WasabiHttpClientFactory httpClientFactory, Network network)
	{
		string apiUrl = network switch
		{
			_ when network == Network.Main => ApiUrl,
			_ when network == Network.TestNet => TestNetApiUrl,
			_ => throw new NotSupportedException($"Unsupported network: {network}")
		};

		// Mempool testnet from Tor works unreliable - 503 (Service Unavailable).
		HttpClient = httpClientFactory.NewHttpClient(
			() => new Uri(apiUrl),
			Tor.Socks5.Pool.Circuits.Mode.NewCircuitPerRequest);
	}

	public async Task<AllFeeEstimate> GetFeeRatesAsync(CancellationToken cancellationToken)
	{
		using var response = await HttpClient.SendAsync(HttpMethod.Get, "fees/recommended", null, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		return ParseFeeEstimates(json);
	}

	/// <summary>
	/// Parses the JSON response from mempool.space API to extract fee estimates.
	/// </summary>
	/// <param name="json">The JSON string containing fee estimates.</param>
	/// <returns>An AllFeeEstimate object containing the parsed fees.</returns>
	private AllFeeEstimate ParseFeeEstimates(string json)
	{
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;
		var feeEstimates = new Dictionary<int, FeeRate>();

		// According to mempool.space docs, the JSON response is similar to:
		// {
		//   "fastestFee": number,
		//   "halfHourFee": number,
		//   "hourFee": number,
		//   "economyFee": number,
		//   "minimumFee": number
		// }
		// We iterate through predefined mappings to extract the relevant fees.

		// Iterate through the defined mappings
		foreach (var mapping in FeeMappings)
		{
			// Check if the property exists and is a number
			if (root.TryGetProperty(mapping.JsonKey, out var jsonElement) &&
				jsonElement.ValueKind == JsonValueKind.Number && // Ensure it's a number type
				jsonElement.TryGetDecimal(out decimal feeValue))   // Try to parse it as decimal
			{
				// Calculate fee rate in sat/vB (rounding up) and add to the dictionary
				// using the target block confirmation time as the key.
				feeEstimates[mapping.TargetBlocks] = new FeeRate(feeValue);
			}
			// If the property doesn't exist or isn't a valid number, it's skipped.
		}

		// Note: The "minimumFee" from the API is ignored in this implementation
		// as it wasn't included in the original logic or the FeeMappings.

		return new AllFeeEstimate(feeEstimates);
	}
}
