using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Settings.ViewModels;

[AppLifetime]
[NavigationMetaData(
	Order = 0,
	Category = SearchCategory.Settings,
	IconName = "settings_general_regular",
	IsLocalized = true)]
public partial class AppearanceSettingsTabViewModel : RoutableViewModel
{
	public AppearanceSettingsTabViewModel(UiContext uiContext, IApplicationSettings settings)
	{
		UiContext = uiContext;
		Settings = settings;
		ExchangeCurrencies = uiContext.AmountProvider.SupportedCurrencies.OrderBy(x => x);
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	public IEnumerable<FeeDisplayUnit> FeeDisplayUnits =>
		Enum.GetValues(typeof(FeeDisplayUnit)).Cast<FeeDisplayUnit>();

	public IEnumerable<DisplayLanguage> DisplayLanguagesList => Enum.GetValues(typeof(DisplayLanguage)).Cast<DisplayLanguage>();

	public IOrderedEnumerable<string> ExchangeCurrencies { get; }
}
