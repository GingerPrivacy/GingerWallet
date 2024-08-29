using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services;

public class TwoFactorAuthenticationService
{
	private string SecretFileName => "2fa_info.gws";
	private string SecretFilePath => Path.Combine(WalletDirectories.WalletsDir, SecretFileName);

	public TwoFactorAuthenticationService(WalletDirectories walletDirectories, WasabiClient wasabiClient)
	{
		WalletDirectories = walletDirectories;
		WasabiClient = wasabiClient;
		IsTwoFactorAuthEnabled = File.Exists(SecretFilePath);

		if (IsTwoFactorAuthEnabled)
		{
			string jsonContent = File.ReadAllText(SecretFilePath);
			var secret = JsonConvert.DeserializeObject<WalletEncryption>(jsonContent);
			ClientId = secret?.ClientId;
			SecretServer = secret?.SecretServer;
		}
	}

	public WalletDirectories WalletDirectories { get; }
	public WasabiClient WasabiClient { get; }
	public bool IsTwoFactorAuthEnabled { get; }

	public string? ClientId { get; }
	public string? SecretServer { get; }

	/// <summary>
	/// The wallet file encryption key.
	/// </summary>
	public string? SecretWallet { get; private set; }

	public void MakeSureWalletFilesAreEncrypted(string secret)
	{
		foreach (var walletFileInfo in WalletDirectories.EnumerateWalletFiles())
		{
			KeyManager? keyManager = null;
			try
			{
				keyManager = KeyManager.FromFile(walletFileInfo.FullName, secret);
				continue;
			}
			catch (JsonSerializationException)
			{
				// Invalid JSON, likely the wallet file was not encrpyted.
			}

			// Try to read as plain text.
			keyManager = KeyManager.FromFile(walletFileInfo.FullName, null);

			var (walletFilePath, walletBackupFilePath) = WalletDirectories.GetWalletFilePaths(keyManager.WalletName);

			Logger.LogInfo($"Wallet file was not encrypted '{walletFileInfo.Name}', encrypting... ");
			keyManager.ToFile(walletFilePath, secret);
			keyManager.ToFile(walletBackupFilePath, secret);
		}
	}

	public async Task LoginVerifyAsync(string token)
	{
		if (ClientId is not { } clientId)
		{
			throw new ArgumentNullException(nameof(ClientId));
		}

		if (SecretServer is not { } secretServer)
		{
			throw new ArgumentNullException(nameof(SecretServer));
		}

		TwoFactorVerifyResponse? response = await WasabiClient
			.VerifyTwoFactorAuthenticationAsync(
			new VerifyTwoFactorModel()
			{
				Token = token,
				ClientId = clientId,
				ServerSecret = secretServer
			})
			.ConfigureAwait(false);

		if (response is null)
		{
			throw new InvalidOperationException($"{nameof(TwoFactorVerifyResponse)} is null.");
		}

		SecretWallet = response.SecretWallet;

		// If this is the first time we login with 2FA, we encrypt the wallets.
		MakeSureWalletFilesAreEncrypted(SecretWallet);
	}

	public async Task VerifyAndSaveClientFileAsync(string token, string clientId, string secretServer)
	{
		TwoFactorVerifyResponse? response = await WasabiClient
			.VerifyTwoFactorAuthenticationAsync(
			new VerifyTwoFactorModel()
			{
				Token = token,
				ClientId = clientId,
				ServerSecret = secretServer
			})
			.ConfigureAwait(false);

		if (response is null)
		{
			throw new InvalidOperationException($"{nameof(TwoFactorVerifyResponse)} is null.");
		}

		WalletEncryption twoFactorInfo = new WalletEncryption
		{
			ClientId = response.ClientId,
			SecretServer = response.ServerSecret
		};

		string twoFactorInfoJson = JsonConvert.SerializeObject(twoFactorInfo);
		await File.WriteAllTextAsync(SecretFilePath, twoFactorInfoJson).ConfigureAwait(false);
	}

	public void RemoveTwoFactorAuthentication()
	{
		foreach (var walletFileInfo in WalletDirectories.EnumerateWalletFiles())
		{
			var keyManager = KeyManager.FromFile(walletFileInfo.FullName, SecretWallet);

			var (walletFilePath, walletBackupFilePath) = WalletDirectories.GetWalletFilePaths(keyManager.WalletName);

			Logger.LogInfo($"Wallet file was not encrypted '{walletFileInfo.Name}', encrypting... ");
			keyManager.ToFile(walletFilePath, null);
			keyManager.ToFile(walletBackupFilePath, null);
		}

		if (File.Exists(SecretFilePath))
		{
			File.Delete(SecretFilePath);
		}
	}
}
