using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Models;

public record InputBannedExceptionData(
	DateTimeOffset BannedUntil,
	InputBannedReasonEnum[]? InputBannedReasonEnums = null) : ExceptionData;
