namespace WalletWasabi.Backend.Models.Responses;

public class TwoFactorVerifyResponse
{
	public required string SecretWallet { get; set; }

	public required string ClientServerId { get; set; }
}
