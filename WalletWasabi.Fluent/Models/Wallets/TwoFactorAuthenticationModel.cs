using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Backend.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.Fluent.Models.UI;
using System.IO;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class TwoFactorAuthenticationModel
{
	private TwoFactorAuthenticationService Service => Services.TwoFactorAuthenticationService;

	public Task LoginVerifyAsync(string token)
	{
		return Service.LoginVerifyAsync(token);
	}

	public Task VerifyAndSaveClientFileAsync(string token, string clientId, string serverSecret)
	{
		return Service.VerifyAndSaveClientFileAsync(token, clientId, serverSecret);
	}

	public Task<TwoFactorSetupResponse> SetupTwoFactorAuthentication()
	{
		return Service.WasabiClient.SetupTwoFactorAuthenticationAsync();
	}

	public void RemoveTwoFactorAuthentication() => Service.RemoveTwoFactorAuthentication();
}
