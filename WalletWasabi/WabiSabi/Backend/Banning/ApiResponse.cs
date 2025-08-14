namespace WalletWasabi.WabiSabi.Backend.Banning;

public record ApiResponse(ApiResponseInfo Info, string Provider, bool ShouldBan, bool ShouldRemove, string Details, TimeSpan RecommendedBanTime);
