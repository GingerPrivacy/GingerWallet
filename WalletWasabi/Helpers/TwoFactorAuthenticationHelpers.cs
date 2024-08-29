using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WalletWasabi.Backend.Models.Responses;

namespace WalletWasabi.Helpers;

public class WalletEncryption
{
	public string ClientId { get; set; }

	public string SecretServer { get; set; }
}

public static class TwoFactorAuthenticationHelpers
{
	public static async Task EncryptWalletFilesAsync(string walletDirectory, string secret)
	{
		foreach (string filePath in Directory.GetFiles(walletDirectory, "*.json"))
		{
			string content = await File.ReadAllTextAsync(filePath);
			string encryptedContent = EncryptString(content, secret);
			await File.WriteAllTextAsync(filePath, encryptedContent);
		}
	}

	public static async Task DecryptWalletFilesAsync(string walletDirectory, string secret)
	{
		foreach (string filePath in Directory.GetFiles(walletDirectory, "*.json"))
		{
			if (Path.GetFileName(filePath) != "2fa_info.gws")
			{
				string content = await File.ReadAllTextAsync(filePath);
				string decryptedContent = DecryptString(content, secret);
				await File.WriteAllTextAsync(filePath, decryptedContent);
			}
		}

		// Delete the 2fa_info.json file
		string twoFactorInfoPath = Path.Combine(walletDirectory, "2fa_info.gws");
		if (File.Exists(twoFactorInfoPath))
		{
			File.Delete(twoFactorInfoPath);
		}
	}

	public static string EncryptString(string plainText, string secret)
	{
		using Aes aes = Aes.Create();
		aes.Key = GetEncryptionKey(secret);
		aes.IV = new byte[16];

		using MemoryStream memoryStream = new();
		using (ICryptoTransform encryptor = aes.CreateEncryptor())
		using (CryptoStream cryptoStream = new(memoryStream, encryptor, CryptoStreamMode.Write))
		{
			byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
			cryptoStream.Write(plainBytes, 0, plainBytes.Length);
		}

		return Convert.ToBase64String(memoryStream.ToArray());
	}

	public static string DecryptString(string cipherText, string secret)
	{
		using Aes aes = Aes.Create();
		aes.Key = GetEncryptionKey(secret);
		aes.IV = new byte[16];

		using MemoryStream memoryStream = new(Convert.FromBase64String(cipherText));
		using ICryptoTransform decryptor = aes.CreateDecryptor();
		using CryptoStream cryptoStream = new(memoryStream, decryptor, CryptoStreamMode.Read);
		using StreamReader streamReader = new(cryptoStream);
		return streamReader.ReadToEnd();
	}

	private static byte[] GetEncryptionKey(string secret)
	{
		using SHA256 sha256 = SHA256.Create();
		return sha256.ComputeHash(Encoding.UTF8.GetBytes(secret));
	}
}
