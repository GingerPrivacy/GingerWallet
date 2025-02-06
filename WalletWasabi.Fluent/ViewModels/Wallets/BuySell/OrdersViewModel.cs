using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Controls.Sorting;
using WalletWasabi.Fluent.Models.BuySell;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.ViewModels.Wallets.BuySell;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class OrdersViewModel : RoutableViewModel
{
	private readonly IBuyModel _buyModel;
	[AutoNotify] private FlatTreeDataGridSource<OrderViewModel> _source = new([]);

	private OrdersViewModel(IBuyModel buyModel)
	{
		_buyModel = buyModel;
		Title = Resources.PreviousOrders;

		EnableBack = true;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public IEnumerable<SortableItem>? Sortables { get; private set; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		IsBusy = true;

		_buyModel.Orders
			.ToObservableChangeSet(x => x.OrderId)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Transform(x => new OrderViewModel(UiContext, x, _buyModel))
			.Sort(SortExpressionComparer<OrderViewModel>.Descending(x => x.Model.CreatedAt))
			.Bind(out var previousOrders)
			.Subscribe()
			.DisposeWith(disposables);

		var source = BuyOrdersDataGridSource.Create(previousOrders);

		Source = source;
		Source.RowSelection!.SingleSelect = true;
		Source.DisposeWith(disposables);

		IsBusy = false;

		Sortables =
		[
			new SortableItem(Resources.Status)
			{
				SortByAscendingCommand = ReactiveCommand.Create(() => ((ITreeDataGridSource) Source).SortBy(Source.Columns[0], ListSortDirection.Ascending)),
				SortByDescendingCommand = ReactiveCommand.Create(() => ((ITreeDataGridSource) Source).SortBy(Source.Columns[0], ListSortDirection.Descending))
			},
			new SortableItem(Resources.Date)
			{
				SortByAscendingCommand = ReactiveCommand.Create(() => ((ITreeDataGridSource) Source).SortBy(Source.Columns[1], ListSortDirection.Ascending)),
				SortByDescendingCommand = ReactiveCommand.Create(() => ((ITreeDataGridSource) Source).SortBy(Source.Columns[1], ListSortDirection.Descending))
			},
			new SortableItem(Resources.Amount)
			{
				SortByAscendingCommand = ReactiveCommand.Create(() => ((ITreeDataGridSource) Source).SortBy(Source.Columns[2], ListSortDirection.Ascending)),
				SortByDescendingCommand = ReactiveCommand.Create(() => ((ITreeDataGridSource) Source).SortBy(Source.Columns[2], ListSortDirection.Descending))
			},
			new SortableItem(Resources.Provider)
			{
				SortByAscendingCommand = ReactiveCommand.Create(() => ((ITreeDataGridSource) Source).SortBy(Source.Columns[3], ListSortDirection.Ascending)),
				SortByDescendingCommand = ReactiveCommand.Create(() => ((ITreeDataGridSource) Source).SortBy(Source.Columns[3], ListSortDirection.Descending))
			},
		];
	}
}
