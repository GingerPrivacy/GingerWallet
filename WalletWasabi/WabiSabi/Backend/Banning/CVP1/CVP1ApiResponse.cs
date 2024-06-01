using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WabiSabi.Backend.Banning.CVP1;

public class CVP1ApiResponse : ApiResponse
{
	public CVP1ApiResponse(CVP1ApiResponseItem responseItem, ApiResponseInfo? info = null) : base(info)
	{
		Response = responseItem;
	}

	public CVP1ApiResponseItem Response { get; }

	public override void Evaluate(int blockchainHeightOfCoin, CoinVerifierRiskConfig riskConfig)
	{
		var riskFlags = riskConfig.RiskFlags;
		if (riskFlags.Count == 0)
		{
			ShouldBan = ShouldRemove = false;
			return;
		}

		var response = Response;
		var flagIds = response.Cscore_section.Cscore_info.Select(cscores => cscores.Id);

		if (flagIds.Except(riskFlags).Any())
		{
			var unknownIds = flagIds.Except(riskFlags).ToList();
			unknownIds.ForEach(id => Logger.LogWarning($"Flag {id} is unknown for the backend!"));
		}

		ShouldBan = flagIds.Any(id => riskFlags.Contains(id));

		// When to remove:
		ShouldRemove = ShouldBan || // If we ban it.
			!response.Report_info_section.Address_used || // If address_used is false (API provider doesn't know about it).
			blockchainHeightOfCoin > response.Report_info_section.Report_block_height; // If the report_block_height is less than the block height of the coin. This means that the API provider didn't processed it, yet. On equal or if the report_height is bigger,then the API provider processed that block for sure.
	}

	public override string GetDetails()
	{
		var response = Response;

		var reportId = response?.Report_info_section.Report_id;
		var reportHeight = response?.Report_info_section.Report_block_height.ToString();
		var reportType = response?.Report_info_section.Report_type;
		var ids = response?.Cscore_section.Cscore_info?.Select(x => x.Id) ?? Enumerable.Empty<int>();
		var categories = response?.Cscore_section.Cscore_info.Select(x => x.Name) ?? Enumerable.Empty<string>();
		var addressUsed = response?.Report_info_section.Address_used ?? false;

		var detailsArray = new string[]
		{
					reportId ?? "ReportID None",
					reportHeight ?? "ReportHeight None",
					reportType ?? "ReportType None",
					addressUsed ? "Address used" : "Address not used",
					ids.Any() ? string.Join(' ', ids) : "FlagIds None",
					categories.Any() ? string.Join(' ', categories) : "Risk categories None"
		};

		// Separate the different values of the ApiResponseItem with '|', so the details will be one value in the CSV file.
		return string.Join("|", detailsArray);
	}

	public static async Task<ApiResponse> ParseResponseAsync(HttpResponseMessage response)
	{
		return new CVP1ApiResponse(await response.Content.ReadAsJsonAsync<CVP1ApiResponseItem>().ConfigureAwait(false));
	}
}
