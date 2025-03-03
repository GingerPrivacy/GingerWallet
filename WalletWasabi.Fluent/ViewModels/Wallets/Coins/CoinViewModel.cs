using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coins;

public partial class CoinViewModel : CoinListItem
{
    private CoinViewModel(LabelsArray labels, ICoinModel coin, bool canSelectWhenCoinjoining, bool ignorePrivacyMode, bool allowSelection)
	{
		Labels = labels;
		Coin = coin;
		AllowSelection = allowSelection;
		Amount = coin.Amount;
		IsConfirmed = coin.IsConfirmed;
		IsBanned = coin.IsBanned;
		var confirmationCount = coin.Confirmations;
		ConfirmationStatus = string.Format(CultureInfo.InvariantCulture, Resources.ConfirmationCount, confirmationCount, TextHelpers.AddSIfPlural(confirmationCount));
		BannedUntilUtcToolTip = coin.BannedUntilUtcToolTip;
		AnonymityScore = coin.AnonScore;
		BannedUntilUtc = coin.BannedUntilUtc;
		IsSelected = false;
		ScriptType = coin.ScriptType;
		IgnorePrivacyMode = ignorePrivacyMode;
		this.WhenAnyValue(x => x.Coin.IsExcludedFromCoinJoin).BindTo(this, x => x.IsExcludedFromCoinJoin).DisposeWith(_disposables);
		this.WhenAnyValue(x => x.Coin.IsCoinJoinInProgress).BindTo(this, x => x.IsCoinjoining).DisposeWith(_disposables);
		this.WhenAnyValue(x => x.CanBeSelected)
			.Where(b => !b)
			.Do(_ => IsSelected = false)
			.Subscribe();

        if (!canSelectWhenCoinjoining)
        {
            this.WhenAnyValue(x => x.Coin.IsCoinJoinInProgress, b => !b).BindTo(this, x => x.CanBeSelected).DisposeWith(_disposables);
        }

        ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().CoinDetails(coin));
	}

	public ICoinModel Coin { get; }
	public override string Key => Coin.Key.ToString();
}
