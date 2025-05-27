using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Lang;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.AddWallet.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class WelcomePageViewModel : DialogViewModelBase<Unit>
{
	private const int NumberOfPages = 2;
	[AutoNotify] private int _selectedIndex;
	[AutoNotify] private string? _nextLabel;
	[AutoNotify] private bool _enableNextKey = true;
	[AutoNotify] private bool _isRestartNeeded;
	[AutoNotify] private DisplayLanguage _selectedDisplayLanguage;

	public WelcomePageViewModel()
	{
		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);

		SelectedIndex = 0;
		NextCommand = ReactiveCommand.Create(OnNext);
		CanGoBack = this.WhenAnyValue(x => x.SelectedIndex, i => i > 0);
		BackCommand = ReactiveCommand.Create(() => SelectedIndex--, CanGoBack);
		_selectedDisplayLanguage = Settings.SelectedDisplayLanguage;

		this.WhenAnyValue(x => x.SelectedIndex)
			.Subscribe(
				x =>
				{
					NextLabel = x < NumberOfPages - 1 ? Resources.Continue : Resources.GetStarted;
					EnableNextKey = x < NumberOfPages - 1;
				});

		this.WhenAnyValue(x => x.IsActive)
			.Skip(1)
			.Where(x => !x)
			.Subscribe(x => EnableNextKey = false);

		this.WhenAnyValue(x => x.SelectedDisplayLanguage)
			.Skip(1)
			.Subscribe(lang =>
			{
				var cultureName = lang.GetDescription() ?? "en-US";
				var culture = CultureInfo.GetCultureInfo(cultureName);
				var currencyCode = culture.GuessPreferredCurrencyCode(UiContext.AmountProvider.SupportedCurrencies);

				Settings.SelectedDisplayLanguage = lang;
				Settings.SelectedExchangeCurrency = currencyCode;
			});
	}

	public IObservable<bool> CanGoBack { get; }

	public IEnumerable<DisplayLanguage> DisplayLanguagesList => Enum.GetValues(typeof(DisplayLanguage)).Cast<DisplayLanguage>().OrderBy(x => x.ToLocalTranslation());

	public ApplicationSettings Settings => UiContext.ApplicationSettings;

	private void OnNext()
	{
		if (IsRestartNeeded)
		{
			AppLifetimeHelper.Shutdown(withShutdownPrevention: false, restart: true);
		}
		else if (SelectedIndex < NumberOfPages - 1)
		{
			SelectedIndex++;
		}
		else if (!UiContext.WalletRepository.HasWallet)
		{
			UiContext.Navigate().To().AddWalletPage();
		}
		else
		{
			Close();
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		UiContext.ApplicationSettings.IsRestartNeeded
			.BindTo(this, x => x.IsRestartNeeded)
			.DisposeWith(disposables);
	}
}
