using System.Net;

namespace WalletWasabi.WabiSabi.Backend.Banning;
/// <summary>
/// Additional info object aside from the provider specific ApiResponseItem
/// </summary>
public record ApiResponseInfo(HttpStatusCode StatusCode, bool SuccessfulParse, string ErrorName, string ErrorDetails)
{
	public string ErrorToString()
	{
		if (ErrorName.Length > 0 || ErrorDetails.Length > 0)
		{
			return $"{ErrorName}:{ErrorDetails}";
		}
		return "";
	}

	public static readonly ApiResponseInfo OK = new(HttpStatusCode.OK, true, "", "");
}
