using ReactiveUI;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class TwoFactorAuthenticationModel : ReactiveObject
{
	public TwoFactorAuthenticationModel()
	{
		TwoFactorEnabled = TwoFactorAuthenticationService.TwoFactorEnabled;
	}

	private TwoFactorAuthenticationService Service => Services.TwoFactorAuthenticationService;

	public Task<TwoFactorSetupResponse> SetupTwoFactorAuthentication()
	{
		return Service.WasabiClient.SetupTwoFactorAuthenticationAsync();
	}

	public async Task VerifyAndSaveClientFileAsync(string token, string clientServerId)
	{
		await Service.VerifyAndSaveClientFileAsync(token, clientServerId);
		this.RaisePropertyChanged(nameof(TwoFactorEnabled));
	}

	public Task LoginVerifyAsync(string token)
	{
		return Service.LoginVerifyAsync(token);
	}

	public void RemoveTwoFactorAuthentication()
	{
		Service.RemoveTwoFactorAuthentication(Services.WalletManager);
		this.RaisePropertyChanged(nameof(TwoFactorEnabled));
	}

	[AutoNotify]
	private bool _twoFactorEnabled;
}
