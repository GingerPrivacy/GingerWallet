using NBitcoin;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Daemon.FeeRateProviders;

/// <summary>
/// A fee rate provider that uses the Blockstream.info API to retrieve fee rate estimates.
/// </summary>
public class BlockstreamInfoFeeRateProvider : IFeeRateProvider
{
	private readonly IHttpClient _httpClient;

	/// <summary>
	/// Initializes a new instance of the <see cref="BlockstreamInfoFeeRateProvider"/> class.
	/// </summary>
	/// <param name="httpClientFactory">The HTTP client factory used to create an HTTP client.</param>
	/// <param name="isTestNet">Specifies whether to use the TestNet endpoint.</param>
	public BlockstreamInfoFeeRateProvider(WasabiHttpClientFactory httpClientFactory, Network network)
	{
		string apiUrl = network switch
		{
			_ when network == Network.Main => httpClientFactory.IsTorEnabled
				? "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion/"
				: "https://blockstream.info",
			_ when network == Network.TestNet => httpClientFactory.IsTorEnabled
				? "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion/testnet/"
				: "https://blockstream.info/testnet",
			_ => throw new NotSupportedException($"Unsupported network: {network}")
		};

		_httpClient = httpClientFactory.NewHttpClient(() => new Uri(apiUrl), Mode.NewCircuitPerRequest);
	}

	/// <summary>
	/// Retrieves fee rate estimations from the Blockstream.info API.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>An <see cref="AllFeeEstimate"/> instance containing the fee estimations.</returns>
	public async Task<AllFeeEstimate> GetFeeRatesAsync(CancellationToken cancellationToken)
	{
		using HttpResponseMessage response = await _httpClient
			.SendAsync(HttpMethod.Get, "api/fee-estimates", null, cancellationToken)
			.ConfigureAwait(false);

		response.EnsureSuccessStatusCode();
		var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		return ParseFeeEstimates(json);
	}

	/// <summary>
	/// Parses the JSON response from Blockstream.info into fee estimates.
	/// </summary>
	/// <param name="json">The JSON response string.</param>
	/// <returns>An <see cref="AllFeeEstimate"/> instance with the mapped fee estimates.</returns>
	private AllFeeEstimate ParseFeeEstimates(string json)
	{
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;
		var feeEstimates = new Dictionary<int, FeeRate>();

		// Blockstream.info returns a JSON object where each property name is the target confirmation block
		// (e.g., "2", "3", "6", "12", etc.) and its value is the estimated fee rate.
		foreach (var property in root.EnumerateObject())
		{
			if (int.TryParse(property.Name, out int target))
			{
				feeEstimates[target] = new FeeRate(property.Value.GetDecimal());
			}
		}

		return new AllFeeEstimate(feeEstimates);
	}
}
