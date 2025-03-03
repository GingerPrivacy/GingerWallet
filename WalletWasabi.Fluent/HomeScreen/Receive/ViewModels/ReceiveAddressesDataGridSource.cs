using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.HomeScreen.Receive.Views.Columns;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.Receive.ViewModels;

public static class ReceiveAddressesDataGridSource
{
	public static FlatTreeDataGridSource<AddressViewModel> Create(IEnumerable<AddressViewModel> addresses)
	{
		return new FlatTreeDataGridSource<AddressViewModel>(addresses)
		{
			Columns =
			{
				TypeColumn(),
				AddressColumn(),
				LabelsColumn(),
				ActionsColumn(),
			}
		};
	}

	private static IColumn<AddressViewModel> TypeColumn()
	{
		return new TemplateColumn<AddressViewModel>(
			null,
			new FuncDataTemplate<AddressViewModel>((node, ns) => new TypeColumnView(), true),
			options: new TemplateColumnOptions<AddressViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = Sort<AddressViewModel>.Ascending(x => x.Type),
				CompareDescending = Sort<AddressViewModel>.Descending(x => x.Type)

			},
			width: new GridLength(0, GridUnitType.Auto));
	}

	private static IColumn<AddressViewModel> ActionsColumn()
	{
		return new TemplateColumn<AddressViewModel>(
			null,
			new FuncDataTemplate<AddressViewModel>((node, ns) => new ActionsColumnView(), true),
			null,
			options: new TemplateColumnOptions<AddressViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = false
			},
			width: new GridLength(0, GridUnitType.Auto));
	}

	private static IColumn<AddressViewModel> AddressColumn()
	{
		return new TemplateColumn<AddressViewModel>(
			Resources.Address,
			new FuncDataTemplate<AddressViewModel>((_, _) => new AddressColumnView(), true),
			null,
			options: new TemplateColumnOptions<AddressViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = Sort<AddressViewModel>.Ascending(x => x.AddressText),
				CompareDescending = Sort<AddressViewModel>.Descending(x => x.AddressText)
			},
			width: new GridLength(0, GridUnitType.Auto));
	}

	private static IColumn<AddressViewModel> LabelsColumn()
	{
		return new TemplateColumn<AddressViewModel>(
			Resources.Label,
			new FuncDataTemplate<AddressViewModel>((_, _) => new LabelsColumnView(), true),
			null,
			options: new TemplateColumnOptions<AddressViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = Sort<AddressViewModel>.Ascending(x => x.Labels),
				CompareDescending = Sort<AddressViewModel>.Descending(x => x.Labels)
			},
			width: new GridLength(1, GridUnitType.Star));
	}
}
