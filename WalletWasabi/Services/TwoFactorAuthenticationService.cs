using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services;

public class TwoFactorAuthenticationService
{
	private string SecretFileName => "2fa_info.gws";
	private static string SecretFilePath { get; set; }

	public TwoFactorAuthenticationService(WalletDirectories walletDirectories, WasabiClient wasabiClient)
	{
		WalletDirectories = walletDirectories;
		SecretFilePath = Path.Combine(WalletDirectories.WalletsDir, SecretFileName);
		WasabiClient = wasabiClient;
		IsTwoFactorAuthEnabled = File.Exists(SecretFilePath);

		if (IsTwoFactorAuthEnabled)
		{
			string jsonContent = File.ReadAllText(SecretFilePath);
			var secret = JsonConvert.DeserializeObject<WalletEncryption>(jsonContent);
			ClientServerId = secret?.ClientServerId;
		}
	}

	public WalletDirectories WalletDirectories { get; }
	public WasabiClient WasabiClient { get; }
	public bool IsTwoFactorAuthEnabled { get; }

	public string? ClientServerId { get; }

	/// <summary>
	/// The wallet file encryption key.
	/// </summary>
	public string? SecretWallet { get; private set; }

	public static bool TwoFactorEnabled => File.Exists(SecretFilePath);

	public void MakeSureWalletFilesAreEncrypted(string secret)
	{
		foreach (var walletFileInfo in WalletDirectories.EnumerateWalletFiles())
		{
			KeyManager? keyManager;
			try
			{
				keyManager = KeyManager.FromFile(walletFileInfo.FullName, secret);
				continue;
			}
			catch (IOException)
			{
				throw;
			}
			catch (Exception)
			{
				// Invalid JSON, likely the wallet file was not encrpyted.
			}

			// Try to read as plain text.
			keyManager = KeyManager.FromFile(walletFileInfo.FullName, null);

			var (walletFilePath, walletBackupFilePath) = WalletDirectories.GetWalletFilePaths(keyManager.WalletName);
			keyManager.EncryptionKey = secret;

			Logger.LogInfo($"Wallet file was not encrypted '{walletFileInfo.Name}', encrypting... ");
			keyManager.ToFile(walletFilePath);
			keyManager.ToFile(walletBackupFilePath);
		}
	}

	public async Task LoginVerifyAsync(string token)
	{
		if (ClientServerId is not { } clientServerId)
		{
			throw new ArgumentNullException(nameof(ClientServerId));
		}

		TwoFactorVerifyResponse? response = await WasabiClient
			.VerifyTwoFactorAuthenticationAsync(
			new VerifyTwoFactorModel()
			{
				Token = token,
				ClientServerId = clientServerId,
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

	public async Task VerifyAndSaveClientFileAsync(string token, string clientServerId)
	{
		TwoFactorVerifyResponse? response = await WasabiClient
			.VerifyTwoFactorAuthenticationAsync(
			new VerifyTwoFactorModel()
			{
				Token = token,
				ClientServerId = clientServerId,
			})
			.ConfigureAwait(false);

		if (response is null)
		{
			throw new InvalidOperationException($"{nameof(TwoFactorVerifyResponse)} is null.");
		}

		WalletEncryption twoFactorInfo = new()
		{
			ClientServerId = response.ClientServerId,
		};

		string twoFactorInfoJson = JsonConvert.SerializeObject(twoFactorInfo);
		await File.WriteAllTextAsync(SecretFilePath, twoFactorInfoJson).ConfigureAwait(false);
	}

	public void RemoveTwoFactorAuthentication(WalletManager walletManager)
	{
		foreach (var wallet in walletManager.GetWallets())
		{
			var keyManager = wallet.KeyManager;
			keyManager.EncryptionKey = null;

			var (walletFilePath, walletBackupFilePath) = WalletDirectories.GetWalletFilePaths(wallet.WalletName);
			keyManager.ToFile();
			keyManager.ToFile(walletBackupFilePath);

			Logger.LogInfo($"Wallet file was encrypted '{wallet.WalletName}', decrypting... ");
		}

		if (File.Exists(SecretFilePath))
		{
			File.Delete(SecretFilePath);
		}
	}
}
