using System.Net;

namespace WalletWasabi.WabiSabi.Backend.Banning;
/// <summary>
/// Additional info object aside from the provider specific ApiResponseItem
/// </summary>
public record ApiResponseInfo
(
	HttpStatusCode StatusCode,
	bool SuccessfulParse,
	string ErrorName,
	string ErrorDetails
);
