using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class TwoFactorAuthenticationModel
{
	private TwoFactorAuthenticationService Service => Services.TwoFactorAuthenticationService;

	public Task<TwoFactorSetupResponse> SetupTwoFactorAuthentication()
	{
		return Service.WasabiClient.SetupTwoFactorAuthenticationAsync();
	}

	public Task VerifyAndSaveClientFileAsync(string token, string clientServerId)
	{
		return Service.VerifyAndSaveClientFileAsync(token, clientServerId);
	}

	public Task LoginVerifyAsync(string token)
	{
		return Service.LoginVerifyAsync(token);
	}

	public void RemoveTwoFactorAuthentication() => Service.RemoveTwoFactorAuthentication(Services.WalletManager);
}
