namespace WalletWasabi.WabiSabi.Backend.Banning;

public record ApiResponse(ApiResponseInfo Info, bool ShouldBan, bool ShouldRemove, string Details);
