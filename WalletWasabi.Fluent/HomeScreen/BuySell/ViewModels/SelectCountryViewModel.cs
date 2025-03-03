using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.HomeScreen.BuySell.Models;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class SelectCountryViewModel : DialogViewModelBase<CountrySelection?>
{
	[AutoNotify] private CountryModel? _selectedCountry;
	[AutoNotify] private StateModel? _selectedState;

	[AutoNotify] private IEnumerable<StateModel>? _states;

	public SelectCountryViewModel(IEnumerable<CountryModel> countries, CountrySelection? currentCountry = null)
	{
		Title = Resources.RegionSelection;
		Countries = countries;
		_selectedCountry = Countries.FirstOrDefault(x => x.Code == currentCountry?.CountryCode)
		                   ?? Countries.FirstOrDefault();
		_selectedState = _selectedCountry?.States?.FirstOrDefault(x => x.Code == currentCountry?.StateCode)
		                 ?? SelectedCountry?.States?.FirstOrDefault();

		var nextCanExecute = this.WhenAnyValue(x => x.SelectedCountry, x => x.SelectedState).Select<(CountryModel?, StateModel?), bool>(tup =>
		{
			var (country, state) = tup;

			if (country is null)
			{
				return false;
			}

			if (country.States is not null && state is null)
			{
				return false;
			}

			return true;
		});
		NextCommand = ReactiveCommand.Create(() =>
		{
			if (SelectedCountry is not { } c)
			{
				return;
			}

			Close(DialogResultKind.Normal, new CountrySelection(c.Name, c.Code, SelectedState?.Name, SelectedState?.Code));
		}, nextCanExecute);
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = false;

		this.WhenAnyValue<SelectCountryViewModel, CountryModel?>(x => x.SelectedCountry)
			.WhereNotNull()
			.Do(c =>
			{
				if (c.States is { } states)
				{
					States = states;
					SelectedState = Enumerable.FirstOrDefault<StateModel>(States);
					return;
				}

				States = null;
				SelectedState = null;
			})
			.Subscribe();
	}

	public IEnumerable<CountryModel> Countries { get; }
}
