using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.Views.Wallets.BuySell.Columns;

namespace WalletWasabi.Fluent.ViewModels.Wallets.BuySell;

public static class BuyOrdersDataGridSource
{
	public static FlatTreeDataGridSource<OrderViewModel> Create(IEnumerable<OrderViewModel> orders)
	{
		return new FlatTreeDataGridSource<OrderViewModel>(orders)
		{
			Columns =
			{
				StatusColumn(),
				DateColumn(),
				AmountColumn(),
				LabelsColumn(),
				ActionsColumn()
			}
		};
	}

	private static IColumn<OrderViewModel> StatusColumn()
	{
		return new TemplateColumn<OrderViewModel>(
			null,
			new FuncDataTemplate<OrderViewModel>((_, _) => new StatusColumnView(), true),
			options: new TemplateColumnOptions<OrderViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = Sort<OrderViewModel>.Ascending(x => x.Model.StatusOrderNumber),
				CompareDescending = Sort<OrderViewModel>.Descending(x => x.Model.StatusOrderNumber)
			},
			width: new GridLength(0, GridUnitType.Auto));
	}

	private static IColumn<OrderViewModel> DateColumn()
	{
		return new PrivacyTextColumn<OrderViewModel>(
			null,
			x => x.DateString,
			type: PrivacyCellType.Date,
			options: new ColumnOptions<OrderViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = Sort<OrderViewModel>.Ascending(x => x.Model.CreatedAt),
				CompareDescending = Sort<OrderViewModel>.Descending(x => x.Model.CreatedAt)
			},
			width: new GridLength(0, GridUnitType.Auto),
			numberOfPrivacyChars: 8);
	}

	private static IColumn<OrderViewModel> AmountColumn()
	{
		return new PrivacyTextColumn<OrderViewModel>(
			"",
			x => x.Model.AmountFrom.ToFormattedFiat(x.Model.CurrencyFrom),
			type: PrivacyCellType.Amount,
			options: new ColumnOptions<OrderViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = Sort<OrderViewModel>.Ascending(x => x.Model.AmountFrom),
				CompareDescending = Sort<OrderViewModel>.Descending(x => x.Model.AmountFrom)
			},
			width: new GridLength(0, GridUnitType.Auto),
			numberOfPrivacyChars: 9);
	}

	private static IColumn<OrderViewModel> LabelsColumn()
	{
		return new TemplateColumn<OrderViewModel>(
			"",
			new FuncDataTemplate<OrderViewModel>((_, _) => new OrderLabelsColumnView(), true),
			options: new TemplateColumnOptions<OrderViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = Sort<OrderViewModel>.Ascending(x => x.Labels),
				CompareDescending = Sort<OrderViewModel>.Descending(x => x.Labels)
			},
			width: new GridLength(1, GridUnitType.Star));
	}

	private static IColumn<OrderViewModel> ActionsColumn()
	{
		return new TemplateColumn<OrderViewModel>(
			null,
			new FuncDataTemplate<OrderViewModel>((node, ns) => new OrderActionsColumnView(), true),
			options: new TemplateColumnOptions<OrderViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = false
			},
			width: new GridLength(0, GridUnitType.Auto));
	}
}
