using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BuySell;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Daemon.BuySell;

public class BuyManager : PeriodicRunner
{
	private CancellationTokenSource _modelCancelTokenSource = new();

	public event EventHandler<Wallet>? OrdersUpdated;

	public BuyManager(BuySellClient client, WalletManager walletManager, TimeSpan period)
		: base(period)
	{
		Client = client;
		WalletManager = walletManager;
	}

	private BuySellClient Client { get; }
	private WalletManager WalletManager { get; }

	/// <summary>
	/// Executes an operation with a timeout of 30 seconds and links it with the internal model cancellation token.
	/// </summary>
	private async Task<T> ExecuteWithTimeoutAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken externalCancellation)
	{
		using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_modelCancelTokenSource.Token, timeoutCts.Token, externalCancellation);
		return await operation(linkedCts.Token).ConfigureAwait(false);
	}

	/// <summary>
	/// Periodic action that fetches new orders from the backend,
	/// computes which orders have changed, notifies subscribers,
	/// and writes the cache to disk.
	/// </summary>
	protected override async Task ActionAsync(CancellationToken cancel)
	{
		if (!WalletManager.HasWallet())
		{
			return;
		}

		// Use a linked cancellation token for the 30-second timeout.
		using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancel, timeoutCts.Token);

		var wallets = WalletManager.GetWallets().Where(w => w.IsLoggedIn).ToArray();

		Dictionary<string, (Wallet Wallet, BuySellClientModels.GetOrderResponseItem OrderItem)> orderIdsNeedsRefresh = new();
		foreach (var wallet in wallets)
		{
			foreach (var order in wallet.KeyManager.BuySellWalletData.Orders
				.Where(order => order.Status != BuySellClientModels.OrderStatus.Expired
				|| order.Status != BuySellClientModels.OrderStatus.Complete
				|| order.Status != BuySellClientModels.OrderStatus.Failed
				|| order.Status != BuySellClientModels.OrderStatus.Refunded))
			{
				orderIdsNeedsRefresh.Add(order.OrderId, (wallet, order));
			}
		}

		if (orderIdsNeedsRefresh.Count == 0)
		{
			// There are no orders to fetch.
			return;
		}

		int orderOffset = 0;
		var newOrders = new Dictionary<string, BuySellClientModels.GetOrderResponseItem>();

		// Fetch orders in pages until no more orders are available.
		do
		{
			try
			{
				var ordersResponse = await Client.GetOrderAsync(new BuySellClientModels.GetOrderRequest
				{
					OrderId = orderIdsNeedsRefresh.Keys.ToArray(),
					Offset = orderOffset
				}, linkedCts.Token).ConfigureAwait(false);

				if (ordersResponse.Orders.Count == 0)
				{
					break;
				}

				if (ordersResponse.Offset != orderOffset)
				{
					throw new InvalidOperationException("Offset mismatch.");
				}

				foreach (var order in ordersResponse.Orders)
				{
					newOrders.Add(order.OrderId, order);
				}

				if (ordersResponse.Orders.Count == ordersResponse.Limit)
				{
					orderOffset += ordersResponse.Orders.Count;
				}
				else
				{
					break;
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Logger.LogError(ex, "Error while fetching orders.");
				throw;
			}

			linkedCts.Token.ThrowIfCancellationRequested();
		}
		while (true);

		HashSet<Wallet> walletOrdersUpdated = new();

		foreach (var wallet in wallets)
		{
			List<BuySellClientModels.GetOrderResponseItem> updatedOrderList = new();
			foreach (var orderItem in wallet.KeyManager.Attributes.BuySellWalletData.Orders)
			{
				if (newOrders.TryGetValue(orderItem.OrderId, out var orderResponseItem))
				{
					if (orderItem.UpdatedAt != orderResponseItem.UpdatedAt)
					{
						walletOrdersUpdated.Add(wallet);
						updatedOrderList.Add(orderResponseItem);
					}
					else
					{
						updatedOrderList.Add(orderItem);
					}
				}
			}
			wallet.KeyManager.Attributes.BuySellWalletData.Orders = updatedOrderList.ToArray();
		}

		foreach (var wallet in walletOrdersUpdated)
		{
			OrdersUpdated?.Invoke(this, wallet);
			wallet.KeyManager.ToFile();
		}
	}

	// --- Methods that call the BuySellClient using the common timeout/linked token pattern ---

	public Task<BuySellClientModels.GetAvailableCountriesResponse> GetAvailableCountriesAsync() =>
		ExecuteWithTimeoutAsync(token =>
		{
			var request = new BuySellClientModels.GetAvailableCountriesRequest
			{
				SupportedFlow = BuySellClientModels.SupportedFlow.Buy
			};
			return Client.GetAvailableCountriesAsync(request, token);
		}, CancellationToken.None);

	public Task<BuySellClientModels.GetCurrencyListReponse[]> GetCurrenciesAsync() =>
		ExecuteWithTimeoutAsync(token =>
		{
			var request = new BuySellClientModels.GetCurrencyListRequest
			{
				CurrencyType = BuySellClientModels.CurrencyType.Fiat,
				SupportedFlow = BuySellClientModels.SupportedFlow.Buy
			};
			return Client.GetCurrencyListAsync(request, token);
		}, CancellationToken.None);

	public Task<BuySellClientModels.GetOffersResponse[]> GetOffersAsync(string currencyFrom, decimal amount, string countryCode, string? stateCode) =>
		ExecuteWithTimeoutAsync(token =>
		{
			var request = new BuySellClientModels.GetOffersRequest
			{
				CurrencyFrom = currencyFrom,
				CurrencyTo = "BTC",
				AmountFrom = amount,
				Country = countryCode,
				State = stateCode
			};
			return Client.GetOffersAsync(request, token);
		}, CancellationToken.None);

	public Task<BuySellClientModels.CreateOrderResponse> CreateOrderAsync(
		Wallet wallet,
		string providerCode,
		string currencyFrom,
		string amountFrom,
		string countryCode,
		string? stateCode,
		string walletAddress,
		string paymentMethod) =>
		ExecuteWithTimeoutAsync(token =>
		{
			var request = new BuySellClientModels.CreateOrderRequest
			{
				ProviderCode = providerCode,
				CurrencyFrom = currencyFrom,
				CurrencyTo = "BTC",
				AmountFrom = amountFrom,
				Country = countryCode,
				State = stateCode,
				WalletAddress = walletAddress,
				PaymentMethod = paymentMethod,
				ExternalUserId = Guid.NewGuid().ToString(),
				ExternalOrderId = Guid.NewGuid().ToString()
			};

			Task<BuySellClientModels.CreateOrderResponse> response = Client.CreateOrderAsync(request, token);

			List<BuySellClientModels.GetOrderResponseItem> updatedOrderList = new(wallet.KeyManager.Attributes.BuySellWalletData.Orders);
			var newOrder = new BuySellClientModels.GetOrderResponseItem()
			{
				OrderId = response.Result.OrderId,
				ProviderCode = response.Result.ProviderCode,
				CurrencyFrom = response.Result.CurrencyFrom,
				CurrencyTo = response.Result.CurrencyTo,
				Country = response.Result.Country,
				State = response.Result.State,
				WalletAddress = response.Result.WalletAddress,
				PaymentMethod = response.Result.PaymentMethod,
				Status = BuySellClientModels.OrderStatus.Created,
				RedirectUrl = response.Result.RedirectUrl,
				OrderType = BuySellClientModels.SupportedFlow.Buy,
				UpdatedAt = DateTimeOffset.MinValue,
				CreatedAt = response.Result.CreatedAt,
				AmountFrom = response.Result.AmountFrom
			};

			updatedOrderList.Add(newOrder);
			wallet.KeyManager.Attributes.BuySellWalletData.Orders = updatedOrderList.ToArray();
			wallet.KeyManager.ToFile();

			return response;
		}, CancellationToken.None);

	public Task<BuySellClientModels.ValidateWalletAddressResponse> ValidateAddressAsync(string address) =>
		ExecuteWithTimeoutAsync(token =>
		{
			var request = new BuySellClientModels.ValidateWalletAddressRequest
			{
				Currency = "BTC",
				WalletAddress = address
			};
			return Client.ValidateAddressAsync(request, token);
		}, CancellationToken.None);

	public Task<BuySellClientModels.GetProvidersListReponse[]> GetProviderListAsync() =>
		ExecuteWithTimeoutAsync(token => Client.GetProvidersListAsync(token), CancellationToken.None);

	public Task<(decimal, decimal)> GetLimitsAsync(string currency, string countryCode, string? stateCode) =>
		ExecuteWithTimeoutAsync(async token =>
		{
			var request = new BuySellClientModels.GetLimitsRequest
			{
				CurrencyFrom = currency,
				CurrencyTo = "BTC",
				Country = countryCode,
				State = stateCode
			};

			var result = await Client.GetOrderLimitsAsync(request, token).ConfigureAwait(false);
			var min = result.Select(x => x.Min ?? decimal.MaxValue).Min();
			var max = result.Select(x => x.Max ?? decimal.MinValue).Max();

			return (min, max);
		}, CancellationToken.None);

	public BuySellClientModels.GetOrderResponseItem[] GetOrders(Wallet wallet)
	{
		return wallet.KeyManager.Attributes.BuySellWalletData.Orders.ToArray();
	}

	public override void Dispose()
	{
		_modelCancelTokenSource.Cancel();
		_modelCancelTokenSource.Dispose();
	}
}
