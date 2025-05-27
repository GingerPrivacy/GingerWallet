using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Fluent.Settings.Models;
using WalletWasabi.Lang;
using WalletWasabi.Lang.Models;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Settings.ViewModels;

[AppLifetime]
[NavigationMetaData(
	Order = 1,
	Category = SearchCategory.Settings,
	IconName = "settings_general_regular",
	IsLocalized = true)]
public partial class AppearanceSettingsTabViewModel : RoutableViewModel
{
	[AutoNotify] private SeparatorModel _selectedDecimalSeparator;
	[AutoNotify] private SeparatorModel _selectedGroupSeparator;
	[AutoNotify] private BtcFractionGroupModel _selectedBtcFractionGroup;

	public AppearanceSettingsTabViewModel(ApplicationSettings settings)
	{
		Settings = settings;
		ExchangeCurrencies = UiContext.AmountProvider.SupportedCurrenciesObservable;
		ExchangeCurrencySelectionEnabled = ExchangeCurrencies.Select(x => x.Any());

		_selectedDecimalSeparator = DecimalsSeparators.First(x => x.Char == Settings.SelectedDecimalSeparator);
		_selectedGroupSeparator = GroupSeparators.First(x => x.Char == Settings.SelectedGroupSeparator);
		_selectedBtcFractionGroup = BtcFractionGroups.First(x => x.GroupSizes.SequenceEqual(Settings.SelectedBtcFractionGroup));

		this.WhenAnyValue(x => x.SelectedDecimalSeparator)
			.Skip(1)
			.Subscribe(x => Settings.SelectedDecimalSeparator = x.Char);

		this.WhenAnyValue(x => x.SelectedGroupSeparator)
			.Skip(1)
			.Subscribe(x => Settings.SelectedGroupSeparator = x.Char);

		this.WhenAnyValue(x => x.SelectedBtcFractionGroup)
			.Skip(1)
			.Subscribe(x => Settings.SelectedBtcFractionGroup = x.GroupSizes);
	}

	public IObservable<bool> ExchangeCurrencySelectionEnabled { get; }

	public IObservable<IOrderedEnumerable<string>> ExchangeCurrencies { get; }

	public bool IsReadOnly => Settings.IsOverridden;

	public ApplicationSettings Settings { get; }

	public IEnumerable<FeeDisplayUnit> FeeDisplayUnits =>
		Enum.GetValues(typeof(FeeDisplayUnit)).Cast<FeeDisplayUnit>();

	public IEnumerable<DisplayLanguage> DisplayLanguagesList => Enum.GetValues(typeof(DisplayLanguage)).Cast<DisplayLanguage>().OrderBy(x => x.ToLocalTranslation());

	public IEnumerable<SeparatorModel> DecimalsSeparators =>
		Enum.GetValues(typeof(DecimalSeparator)).Cast<DecimalSeparator>().Select(x => new SeparatorModel(x.FriendlyName(), x.GetChar()));

	public IEnumerable<SeparatorModel> GroupSeparators =>
		Enum.GetValues(typeof(GroupSeparator)).Cast<GroupSeparator>().Select(x => new SeparatorModel(x.FriendlyName(), x.GetChar()));

	public IEnumerable<BtcFractionGroupModel> BtcFractionGroups =>
		Enum.GetValues(typeof(BtcFractionGroupSize)).Cast<BtcFractionGroupSize>().Select(x => new BtcFractionGroupModel(x.FriendlyName(), x.GetGroupSizes()));
}
