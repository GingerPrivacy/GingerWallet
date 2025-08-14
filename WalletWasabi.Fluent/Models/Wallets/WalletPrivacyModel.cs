using System.Linq;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Wallets;
using Unit = System.Reactive.Unit;

namespace WalletWasabi.Fluent.Models.Wallets;

public class WalletPrivacyModel
{
	public WalletPrivacyModel(WalletModel walletModel, Wallet wallet)
	{
		ProgressUpdated =
			walletModel.Transactions.TransactionProcessed
				.Merge(walletModel.Settings.WhenAnyValue(x => x.AnonScoreTarget).ToSignal())
				.ObserveOn(RxApp.MainThreadScheduler)
				.Skip(1);

		Progress = ProgressUpdated.Select(_ => wallet.GetPrivacyPercentage());
		PrivatePercentage = ProgressUpdated.Select(_ =>
		{
			var allCoins = walletModel.Coins.List.Items.ToArray();
			var totalSum = allCoins.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC));

			if (totalSum == 0)
			{
				return 0;
			}

			var privateSum = allCoins.Where(x => x.IsPrivate).Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC));
			return (double)privateSum / (double)totalSum;
		});

		PrivateAndSemiPrivatePercentage = ProgressUpdated.Select(_ =>
		{
			var allCoins = walletModel.Coins.List.Items.ToArray();
			var totalSum = allCoins.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC));

			if (totalSum == 0)
			{
				return 0;
			}

			var privateCoins = allCoins.Where(x => x.IsPrivate);
			var privateSum = privateCoins.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC));

			var semiPrivateCoins = allCoins.Where(x => x.IsSemiPrivate);
			var semiprivateSum = semiPrivateCoins.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC));
			return ((double)privateSum + (double)semiprivateSum) / (double)totalSum;
		});

		IsWalletPrivate = ProgressUpdated.Select(x => wallet.IsWalletPrivate());
	}

	public IObservable<Unit> ProgressUpdated { get; }

	public IObservable<int> Progress { get; }

	public IObservable<double> PrivatePercentage { get; }

	public IObservable<double> PrivateAndSemiPrivatePercentage { get; }

	public IObservable<bool> IsWalletPrivate { get; }
}
