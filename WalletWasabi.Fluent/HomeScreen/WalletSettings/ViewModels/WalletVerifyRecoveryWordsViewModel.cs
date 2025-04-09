using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.Models;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Lang;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.HomeScreen.WalletSettings.ViewModels;

public partial class WalletVerifyRecoveryWordsViewModel : RoutableViewModel
{
	[AutoNotify] private IEnumerable<string>? _suggestions;
	[AutoNotify] private Mnemonic? _currentMnemonics;

	public WalletVerifyRecoveryWordsViewModel(IWalletModel wallet)
	{
		Title = Lang.Resources.VerifyRecoveryWords;

		_suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();

		Mnemonics.ToObservableChangeSet().ToCollection()
			.Select(x => x.Count is 12 or 15 or 18 or 21 or 24 ? new Mnemonic(GetTagsAsConcatString().ToLowerInvariant()) : default)
			.Subscribe(x =>
			{
				CurrentMnemonics = x;
				this.RaisePropertyChanged(nameof(Mnemonics));
			});

		this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);

		EnableBack = true;

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(wallet), this.WhenAnyValue<WalletVerifyRecoveryWordsViewModel, Mnemonic?>(x => x.CurrentMnemonics).Select(x => x is not null));

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableAutoBusyOn(NextCommand);
	}

	public ObservableCollection<string> Mnemonics { get; } = new();

	private bool IsMnemonicsValid => CurrentMnemonics is { IsValidChecksum: true };

	private async Task ShowErrorAsync()
	{
		await ShowErrorAsync(
			Resources.Error,
			Resources.VerifyRecoveryWordsErrorMessage,
			Resources.VerifyRecoveryWordsErrorCaption);
	}

	private async Task OnNextAsync(IWalletModel wallet)
	{
		try
		{
			if (!IsMnemonicsValid || CurrentMnemonics is not { } currentMnemonics)
			{
				await ShowErrorAsync();

				Mnemonics.Clear();
				return;
			}

			var verificationResult = wallet.Auth.VerifyRecoveryWords(currentMnemonics);
			if (verificationResult)
			{
				Navigate().To().Success(navigationMode: NavigationMode.Clear);
			}
			else
			{
				await ShowErrorAsync();
				Mnemonics.Clear();
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(), Resources.UnableToVerifyRecoveryWords);
		}
	}

	private void ValidateMnemonics(IValidationErrors errors)
	{
		if (IsMnemonicsValid)
		{
			return;
		}

		if (!Mnemonics.Any())
		{
			return;
		}

		errors.Add(ErrorSeverity.Error, Resources.InvalidRecoveryWords);
	}

	private string GetTagsAsConcatString()
	{
		return string.Join(' ', Mnemonics);
	}
}
