namespace WalletWasabi.Backend.Models;

public class VerifyTwoFactorModel
{
	public required string ClientServerId { get; set; }
	public required string Token { get; set; }
}
