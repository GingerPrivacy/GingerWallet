using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.BuySell;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.ViewModels.Wallets.BuySell;

public partial class OrderDetailsViewModel : RoutableViewModel
{
	private readonly IBuyModel _buyModel;

	[AutoNotify] private string _orderId = "";
	[AutoNotify] private string _amount = "";
	[AutoNotify] private string _provider = "";
	[AutoNotify] private string _date = "";
	[AutoNotify] private string _status = "";

	private OrderDetailsViewModel(GetOrderModel model, IBuyModel buyModel)
	{
		_buyModel = buyModel;
		Title = Resources.OrderDetails;

		NextCommand = ReactiveCommand.Create(OnNext);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = false;

		UpdateValues(model);
	}

	private void UpdateValues(GetOrderModel model)
	{
		OrderId = model.OrderId;
		Amount = model.AmountFrom.ToFormattedFiat(model.CurrencyFrom);
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

		if (model.IsHeld)
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

		_buyModel.Orders
			.ToObservableChangeSet(x => x.OrderId)
			.Do(_ => UpdateCurrentTransaction())
			.Subscribe()
			.DisposeWith(disposables);
	}

	private void UpdateCurrentTransaction()
	{
		if (_buyModel.Orders.FirstOrDefault(x => x.OrderId == OrderId) is { } updatedModel)
		{
			UpdateValues(updatedModel);
		}
	}
}
