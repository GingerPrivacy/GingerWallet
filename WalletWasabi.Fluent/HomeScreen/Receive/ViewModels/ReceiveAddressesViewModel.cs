using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Controls.Sorting;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.Receive.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class ReceiveAddressesViewModel : RoutableViewModel
{
	private readonly WalletModel _wallet;

	[AutoNotify] private FlatTreeDataGridSource<AddressViewModel> _source = new(Enumerable.Empty<AddressViewModel>());

	public ReceiveAddressesViewModel(WalletModel wallet)
	{
		Title = Resources.AddressesAwaitingPayment;
		_wallet = wallet;

		EnableBack = true;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public IEnumerable<SortableItem>? Sortables { get; private set; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		_wallet.Addresses.Unused
			.ToObservableChangeSet()
			.Transform(CreateAddressViewModel)
			.DisposeMany()
			.Bind(out var unusedAddresses)
			.Subscribe()
			.DisposeWith(disposables);

		var source = ReceiveAddressesDataGridSource.Create(unusedAddresses);

		Source = source;
		Source.RowSelection!.SingleSelect = true;
		Source.DisposeWith(disposables);

		Sortables =
		[
			new SortableItem(Resources.Type) { SortByAscendingCommand = ReactiveCommand.Create(() => ((ITreeDataGridSource) Source).SortBy(Source.Columns[0], ListSortDirection.Ascending)), SortByDescendingCommand = ReactiveCommand.Create(() => ((ITreeDataGridSource) Source).SortBy(Source.Columns[0], ListSortDirection.Descending)) },
			new SortableItem(Resources.Address) { SortByAscendingCommand = ReactiveCommand.Create(() => ((ITreeDataGridSource) Source).SortBy(Source.Columns[1], ListSortDirection.Ascending)), SortByDescendingCommand = ReactiveCommand.Create(() => ((ITreeDataGridSource) Source).SortBy(Source.Columns[1], ListSortDirection.Descending)) },
			new SortableItem(Resources.Label) { SortByAscendingCommand = ReactiveCommand.Create(() => ((ITreeDataGridSource) Source).SortBy(Source.Columns[2], ListSortDirection.Ascending)), SortByDescendingCommand = ReactiveCommand.Create(() => ((ITreeDataGridSource) Source).SortBy(Source.Columns[2], ListSortDirection.Descending)) }
		];

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private AddressViewModel CreateAddressViewModel(AddressModel address)
	{
		return new AddressViewModel(UiContext, OnEditAddressAsync, OnShowAddressAsync, address);
	}

	private void OnShowAddressAsync(AddressModel a)
	{
		UiContext.Navigate().To().ReceiveAddress(_wallet, a, Services.UiConfig.Autocopy);
	}

	public async Task OnEditAddressAsync(AddressModel address)
	{
		var result = await UiContext.Navigate().To().AddressLabelEdit(_wallet, address).GetResultAsync();
		if (result is { } labels)
		{
			address.SetLabels(labels);
		}
	}
}
