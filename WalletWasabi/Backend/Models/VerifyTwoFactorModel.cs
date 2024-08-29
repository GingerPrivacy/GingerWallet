namespace WalletWasabi.Backend.Models;

public class VerifyTwoFactorModel
{
	public string ClientId { get; set; }
	public string ServerSecret { get; set; }
	public string Token { get; set; }
}
