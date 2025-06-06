using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.AddWallet.Models;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Lang;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.AddWallet.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class RecoverWalletViewModel : RoutableViewModel
{
	[AutoNotify] private IEnumerable<string>? _suggestions;
	[AutoNotify] private Mnemonic? _currentMnemonics;
	[AutoNotify] private bool _isMnemonicsValid;

	public RecoverWalletViewModel(WalletCreationOptions.RecoverWallet options)
	{
		Title = Lang.Resources.RecoveryWords;

		Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();

		Mnemonics.ToObservableChangeSet().ToCollection()
			.Select(x => x.Count is 12 or 15 or 18 or 21 or 24 ? new Mnemonic(GetTagsAsConcatString().ToLowerInvariant()) : null)
			.Subscribe(x =>
			{
				CurrentMnemonics = x;
				IsMnemonicsValid = x is { IsValidChecksum: true };
				this.RaisePropertyChanged(nameof(Mnemonics));
			});

		this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);

		EnableBack = true;

		NextCommand = ReactiveCommand.CreateFromTask(
			async () => await OnNextAsync(options),
			canExecute: this.WhenAnyValue(x => x.IsMnemonicsValid));

		AdvancedRecoveryOptionsDialogCommand = ReactiveCommand.CreateFromTask(OnAdvancedRecoveryOptionsDialogAsync);
	}

	public ICommand AdvancedRecoveryOptionsDialogCommand { get; }

	private int MinGapLimit { get; set; } = 114;

	public ObservableCollection<string> Mnemonics { get; } = new();

	private async Task OnNextAsync(WalletCreationOptions.RecoverWallet options)
	{
		var (walletName, _, _, _) = options;
		ArgumentException.ThrowIfNullOrEmpty(walletName);

		var password = await UiContext.Navigate().To().CreatePasswordDialog(Lang.Resources.EnterPassphrase, Lang.Resources.RecoverWalletViewModelPassphraseMessage).GetResultAsync();
		if (password is not { } || CurrentMnemonics is not { IsValidChecksum: true } currentMnemonics)
		{
			return;
		}

		IsBusy = true;

		try
		{
			options = options with { Password = password, Mnemonic = currentMnemonics, MinGapLimit = MinGapLimit };
			var wallet = await UiContext.WalletRepository.NewWalletAsync(options);
			UiContext.Navigate().To().AddedWalletPage(wallet, options);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(), Resources.UnableToRecoverWallet);
		}

		IsBusy = false;
	}

	private async Task OnAdvancedRecoveryOptionsDialogAsync()
	{
		var result = await UiContext.Navigate().To().AdvancedRecoveryOptions(MinGapLimit).GetResultAsync();
		if (result is { } minGapLimit)
		{
			MinGapLimit = minGapLimit;
		}
	}

	private void ValidateMnemonics(IValidationErrors errors)
	{
		if (CurrentMnemonics is null)
		{
			ClearValidations();
			return;
		}

		if (IsMnemonicsValid)
		{
			return;
		}

		if (!Mnemonics.Any())
		{
			return;
		}

		errors.Add(ErrorSeverity.Error, Resources.InvalidSetRecoveryWords);
	}

	private string GetTagsAsConcatString()
	{
		return string.Join(' ', Mnemonics);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}
}
