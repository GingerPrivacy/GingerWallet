using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.BuySell.Models;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.BuySell.ViewModels;

public partial class OrderDetailsViewModel : RoutableViewModel
{
	private readonly ReadOnlyObservableCollection<GetOrderModel> _list;

	[AutoNotify] private string _orderId = "";
	[AutoNotify] private string _amount = "";
	[AutoNotify] private string _provider = "";
	[AutoNotify] private string _date = "";
	[AutoNotify] private string _status = "";

	private OrderDetailsViewModel(GetOrderModel model, IBuySellModel buyModel)
	{
		_list = model.IsBuyOrder ? buyModel.BuyOrders : buyModel.SellOrders;
		Title = Resources.OrderDetails;

		NextCommand = ReactiveCommand.Create(OnNext);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = true;

		UpdateValues(model);
	}

	private void UpdateValues(GetOrderModel model)
	{
		OrderId = model.OrderId;
		Amount = model.GetFormattedAmount();
		Provider = model.ProviderName;
		Date = model.CreatedAt.ToUserFacingString();
		Status = GetStatusText(model);
	}

	private string GetStatusText(GetOrderModel model)
	{
		if (model.IsCreated)
		{
			return Resources.Created;
		}

		if (model.IsPending)
		{
			return Resources.Pending;
		}

		if (model.IsOnHold)
		{
			return Resources.TransactionOnHold;
		}

		if (model.IsRefunded)
		{
			return Resources.Refunded;
		}

		if (model.IsExpired)
		{
			return Resources.Expired;
		}

		if (model.IsFailed)
		{
			return Resources.Failed;
		}

		return Resources.Completed;
	}

	private void OnNext()
	{
		Navigate().Back();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_list
			.ToObservableChangeSet(x => x.OrderId)
			.Do(_ => UpdateCurrentTransaction())
			.Subscribe()
			.DisposeWith(disposables);
	}

	private void UpdateCurrentTransaction()
	{
		if (_list.FirstOrDefault(x => x.OrderId == OrderId) is { } updatedModel)
		{
			UpdateValues(updatedModel);
		}
	}
}
