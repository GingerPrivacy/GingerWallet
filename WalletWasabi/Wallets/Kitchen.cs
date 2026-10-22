using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Wallets;

public class Kitchen
{
	public Kitchen(string? ingredients = null)
	{
		if (ingredients is { })
		{
			Cook(ingredients);
		}
	}

	private string? Salt { get; set; } = null;
	private string? Soup { get; set; } = null;
	private object RefrigeratorLock { get; } = new();

	[MemberNotNullWhen(returnValue: true, nameof(Salt), nameof(Soup))]
	public bool HasIngredients => Salt is not null && Soup is not null;

	public string SaltSoup()
	{
		if (!HasIngredients)
		{
			throw new InvalidOperationException("Ingredients are missing.");
		}

		string res;
		lock (RefrigeratorLock)
		{
			res = StringCipher.Decrypt(Soup, Salt);
		}

		Cook(res);

		return res;
	}

	public void Cook(string ingredients)
	{
		lock (RefrigeratorLock)
		{
			ingredients ??= "";

			Salt = SecureRandom.Instance.GetString(21, Constants.AlphaNumericCharacters);
			Soup = StringCipher.Encrypt(ingredients, Salt);
		}
	}

	public void CleanUp()
	{
		lock (RefrigeratorLock)
		{
			Salt = null;
			Soup = null;
		}
	}
}
