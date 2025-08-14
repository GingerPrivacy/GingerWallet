namespace WalletWasabi.Backend.Models.Responses;

public class TwoFactorSetupResponse
{
	public required string QrCodeUri { get; set; }

	public required string ClientServerId { get; set; }
}
