using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Controls.Sorting;
using WalletWasabi.Fluent.HomeScreen.BuySell.Models;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.DialogScreen)]
public partial class OrdersViewModel : RoutableViewModel
{
	private readonly BuySellModel _buySellModel;
	private readonly OrderType _type;
	[AutoNotify] private FlatTreeDataGridSource<OrderViewModel> _source = new([]);

	public OrdersViewModel(BuySellModel buySellModel, OrderType type)
	{
		_buySellModel = buySellModel;
		_type = type;
		Title = Resources.PreviousOrders;

		EnableBack = true;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public IEnumerable<SortableItem>? Sortables { get; private set; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		IsBusy = true;

		var orders = _type == OrderType.Buy
			? _buySellModel.BuyOrders
			: _buySellModel.SellOrders;

		orders
			.ToObservableChangeSet(x => x.OrderId)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Transform(x => new OrderViewModel(x, _buySellModel))
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
