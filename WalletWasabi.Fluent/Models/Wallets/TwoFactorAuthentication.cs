using ReactiveUI;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class TwoFactorAuthentication : ReactiveObject
{
	[AutoNotify] private bool _twoFactorEnabled;

	public TwoFactorAuthentication()
	{
		StartupValue = TwoFactorAuthenticationService.TwoFactorEnabled;
		TwoFactorEnabled = TwoFactorAuthenticationService.TwoFactorEnabled;
	}

	public bool StartupValue { get; }

	private TwoFactorAuthenticationService Service => Services.TwoFactorAuthenticationService;

	public Task<TwoFactorSetupResponse> SetupTwoFactorAuthentication()
	{
		return Service.WasabiClient.SetupTwoFactorAuthenticationAsync();
	}

	public async Task VerifyAndSaveClientFileAsync(string token, string clientServerId)
	{
		await Service.VerifyAndSaveClientFileAsync(token, clientServerId);
		TwoFactorEnabled = TwoFactorAuthenticationService.TwoFactorEnabled;
	}

	public Task LoginVerifyAsync(string token)
	{
		return Service.LoginVerifyAsync(token);
	}

	public void RemoveTwoFactorAuthentication()
	{
		Service.RemoveTwoFactorAuthentication(Services.WalletManager);
		TwoFactorEnabled = TwoFactorAuthenticationService.TwoFactorEnabled;
	}
}
