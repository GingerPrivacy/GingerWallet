using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Helpers;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.Send.Models;

public record RecipientInfo(BitcoinAddress Destination, Money Amount, LabelsArray Label)
{
	public static bool TryCreate(string addressText, decimal? amountBtc, LabelsArray labels, Network network,
		out RecipientInfo? recipient, out string? error)
	{
		recipient = null;
		error = null;
		BitcoinAddress address;
		try
		{
			address = BitcoinAddress.Create(addressText, network);
		}
		catch (FormatException)
		{
			error = string.IsNullOrEmpty(addressText) ? null : Resources.InvalidBTCAddress;
			return false;
		}

		if (amountBtc is not { } amount || amount <= 0 || amount > Constants.MaximumNumberOfBitcoins)
		{
			error = amountBtc is null ? null : amountBtc > Constants.MaximumNumberOfBitcoins ? Resources.AmountLessThanTotalSupply : Resources.AmountMoreThanZero;
			return false;
		}

		var money = Money.Coins(amount);
		if (money.ToDecimal(MoneyUnit.BTC) != amount)
		{
			error = Resources.BatchAmountPrecision;
			return false;
		}

		recipient = new RecipientInfo(address, money, labels);
		return true;
	}
}
