using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Banning.CVP2;

public class CVP2ApiResponse : ApiResponse
{
	public CVP2ApiResponse(CVP2ApiResponseItem responseItem, ApiResponseInfo? info = null) : base(info)
	{
		Response = responseItem;
	}

	public CVP2ApiResponseItem Response { get; }

	public double Risk { get; protected set; }

	public double RiskMain { get; protected set; }
	public double RiskIllicit { get; protected set; } // Illicit Activity
	public double RiskSanctioned { get; protected set; } // Sanctioned, TF & CSAM
	public double RiskObfuscating { get; protected set; } // Obfuscating & Misc.
	public double RiskUnknown { get; protected set; } // Unknown rule

	public override void Evaluate(int blockchainHeightOfCoin, CoinVerifierRiskConfig riskConfig)
	{
		var response = Response;

		ShouldBan = false;
		Risk = double.NaN;
		RiskMain = response.risk_score ?? double.NaN;
		RiskIllicit = RiskSanctioned = RiskObfuscating = RiskUnknown = 0;

		if (Info.StatusCode != HttpStatusCode.OK)
		{
			ShouldRemove = true;
			return;
		}

		if (!Info.SuccessfulParse)
		{
			CheckRiskAndBan(RiskMain, riskConfig.RiskScoreLimitDefault);
			// We weren't able to parse the result, we don't have risk number either, ban for safety reasons
			if (double.IsNaN(Risk))
			{
				CheckRiskAndBan(riskConfig.RiskScoreLimitDefault, riskConfig.RiskScoreLimitDefault);
			}
		}
		else
		{
			// by default the risk is 0, since fully parsed (the api can give back NaNs even with complete process status)
			CheckRiskAndBan(0, riskConfig.RiskScoreLimitDefault);

			response.evaluation_detail.source.ForEach(EvaluateRule);
			response.evaluation_detail.destination.ForEach(EvaluateRule);

			double riskOther = Math.Max(RiskIllicit, Math.Max(RiskSanctioned, RiskUnknown));
			CheckRiskAndBan(riskOther, riskConfig.RiskScoreLimitDefault);
			CheckRiskAndBan(RiskObfuscating, riskConfig.RiskScoreLimitObfuscating);

			// We didn't have detailed information
			if (RiskMain > Risk)
			{
				CheckRiskAndBan(RiskMain, riskConfig.RiskScoreLimitDefault);
			}
		}

		bool completed = (response.process_status is not null && response.process_status == "complete") || (response.process_status_id is not null && response?.process_status_id == 2);
		ShouldRemove = ShouldBan || !completed;
	}

	private void CheckRiskAndBan(double risk, double limit)
	{
		if (double.IsNaN(risk))
		{
			return;
		}

		if (double.IsNaN(Risk) || Risk < risk)
		{
			Risk = risk;
		}
		if (risk >= limit)
		{
			ShouldBan = true;
		}
	}

	private void EvaluateRule(Rule rule)
	{
		string name = rule.rule_name;
		double risk = rule.risk_score;
		if (name.Contains("illicit", StringComparison.OrdinalIgnoreCase))
		{
			RiskIllicit = Math.Max(RiskIllicit, risk);
		}
		else if (name.Contains("sanctioned", StringComparison.OrdinalIgnoreCase))
		{
			RiskSanctioned = Math.Max(RiskSanctioned, risk);
		}
		else if (name.Contains("obfuscating", StringComparison.OrdinalIgnoreCase))
		{
			RiskObfuscating = Math.Max(RiskObfuscating, risk);
		}
		else
		{
			RiskUnknown = Math.Max(RiskUnknown, risk);
		}
	}

	public override string GetDetails()
	{
		var response = Response;

		var reportId = response.id ?? "ReportID None";
		string reportStatus;
		string reportError;
		if (Info.StatusCode == HttpStatusCode.OK)
		{
			if (response.process_status is not null)
			{
				reportStatus = response.process_status;
			}
			else
			{
				reportStatus = (response.process_status_id ?? 0) switch
				{
					1 => "running",
					2 => "completed",
					3 => "error",
					_ => "invalid"
				};
			}
			if (!Info.SuccessfulParse)
			{
				reportError = $"ParseError {Info.ErrorName ?? ""}:{Info.ErrorDetails}";
			}
			else
			{
				reportError = response.error?.message ?? "";
			}
		}
		else
		{
			reportStatus = $"httpError({Info.StatusCode})";
			reportError = $"HttpError {Info.ErrorName ?? ""}:{Info.ErrorDetails}";
		}
		var reportResult = $"{(ShouldBan ? 'B' : '_')}{(ShouldRemove ? 'R' : '_')}{(!Info.SuccessfulParse ? 'P' : '_')}";

		var reportRiskScore = $"{Risk,6:f3}({RiskMain,6:f3},{RiskSanctioned,6:f3},{RiskIllicit,6:f3},{RiskObfuscating,6:f3})";
		var reportSource = GetRuleDetails(response.evaluation_detail?.source);
		var reportSourceEntities = GetEntityDetails(response.contributions?.source);
		var reportDestination = GetRuleDetails(response.evaluation_detail?.destination);
		var reportDestinationEntities = GetEntityDetails(response.contributions?.destination);

		var detailsArray = new string[]
		{
					reportId,
					reportResult,
					reportStatus,
					reportRiskScore,
					reportSource,
					reportSourceEntities,
					reportDestination,
					reportDestinationEntities,
					reportError
		};

		// Separate the different values of the ApiResponseItem with '|', so the details will be one value in the CSV file.
		return string.Join("|", detailsArray);
	}

	private string GetRuleDetails(List<Rule>? rules)
	{
		if (rules is null)
		{
			return "";
		}
		var res = rules.Select(rule => $"{rule.rule_name}({rule.risk_score,6:f3})").ToList();
		res.Sort();
		return string.Join("/", res);
	}

	private string GetEntityDetails(List<Entities>? entities)
	{
		SortedSet<string> res = new();
		if (entities is not null)
		{
			entities.ForEach(entities => entities?.entities?.ForEach(entity => res.Add(entity?.category ?? "")));
			res.Remove("");
		}
		return string.Join("/", res);
	}

	public static async Task<ApiResponse> ParseResponseAsync(HttpResponseMessage response)
	{
		var jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false) ?? "{}";

		CVP2ApiResponseItem? responseItem;
		ApiResponseInfo? info = null;

		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			var error = JsonConvert.DeserializeObject<CVP2.HttpError>(jsonString, JsonSettings);
			info = new ApiResponseInfo(response.StatusCode, false, error?.name ?? "", error?.message ?? "");
			responseItem = CreateAndCopy<CVP2ApiResponseItem>();
			typeof(CVP2ApiResponseItem).GetProperty("id")?.SetValue(responseItem, error?.id ?? "");
		}
		else
		{
			try
			{
				responseItem = JsonConvert.DeserializeObject<CVP2ApiResponseItem>(jsonString, JsonSettings)
					?? throw new InvalidOperationException("'null' is forbidden.");
			}
			catch (Exception ex)
			{
				var responsePartial = JsonConvert.DeserializeObject<CVP2ApiResponseItemPartial>(jsonString, JsonSettings)
					?? throw new InvalidOperationException("'null' is forbidden.");
				responseItem = CreateAndCopy<CVP2ApiResponseItem>(responsePartial);
				info = new ApiResponseInfo(response.StatusCode, false, "Exception", ex.Message);
			}
		}

		return new CVP2ApiResponse(responseItem, info);
	}
}
