using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BuySell;
using WalletWasabi.Extensions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Daemon.BuySell;

public class BuySellManager : PeriodicRunner
{
	private CancellationTokenSource _modelCancelTokenSource = new();
	private readonly ReaderWriterLockSlim _buySellWalletDataLock = new();

	public event EventHandler<Wallet>? OrdersUpdated;

	public BuySellManager(BuySellClient client, WalletManager walletManager, TimeSpan period)
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

		Dictionary<Wallet, BuySellClientModels.GetOrderResponseItem[]> activeOrdersByWallets = new();

		_buySellWalletDataLock.EnterReadLock();
		try
		{
			foreach (var wallet in wallets)
			{
				var activeOrders = wallet.KeyManager.BuySellWalletData.Orders
					.Where(order => order.Status.IsActive())
					.ToArray();

				if (activeOrders.Length > 0)
				{
					activeOrdersByWallets.Add(wallet, activeOrders);
				}
			}
		}
		finally
		{
			_buySellWalletDataLock.ExitReadLock();
		}

		if (activeOrdersByWallets.Count == 0)
		{
			// There are no orders to fetch.
			return;
		}

		// Fetch orders in pages until no more orders are available.
		var orderIds = activeOrdersByWallets.Values.SelectMany(orders => orders.Select(o => o.OrderId)).ToArray();
		var newOrders = new Dictionary<string, BuySellClientModels.GetOrderResponseItem>();
		int orderOffset = 0;
		do
		{
			var ordersResponse = await Client.GetOrderAsync(new BuySellClientModels.GetOrderRequest
			{
				OrderId = orderIds,
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

			if (ordersResponse.Orders.Count < ordersResponse.Limit)
			{
				break;
			}

			orderOffset += ordersResponse.Orders.Count;
			linkedCts.Token.ThrowIfCancellationRequested();
		} while (true);

		HashSet<Wallet> updatedWallets = [];

		_buySellWalletDataLock.EnterWriteLock();
		try
		{
			foreach (var (wallet, activeOrders) in activeOrdersByWallets)
			{
				var updatedOrders =
					activeOrders
						.Select(o => newOrders.TryGetValue(o.OrderId, out var newOrder) && newOrder.UpdatedAt != o.UpdatedAt
									? newOrder
									: o)
						.ToArray();

				if (!updatedOrders.SequenceEqual(activeOrders))
				{
					var remainingOrders =
						wallet.KeyManager.Attributes.BuySellWalletData.Orders
						.Where(existingOrder => updatedOrders.All(updatedOrder => updatedOrder.OrderId != existingOrder.OrderId));

					wallet.KeyManager.Attributes.BuySellWalletData.Orders = updatedOrders.Union(remainingOrders).ToArray();
					wallet.KeyManager.ToFile();

					updatedWallets.Add(wallet);
				}
			}
		}
		finally
		{
			_buySellWalletDataLock.ExitWriteLock();
		}

		foreach (var wallet in updatedWallets)
		{
			OrdersUpdated?.Invoke(this, wallet);
		}
	}

	// --- Methods that call the BuySellClient using the common timeout/linked token pattern ---

	public Task<BuySellClientModels.GetAvailableCountriesResponse> GetAvailableCountriesAsync(BuySellClientModels.SupportedFlow flow) =>
		ExecuteWithTimeoutAsync(token =>
		{
			var request = new BuySellClientModels.GetAvailableCountriesRequest
			{
				SupportedFlow = flow
			};
			return Client.GetAvailableCountriesAsync(request, token);
		}, CancellationToken.None);

	public Task<BuySellClientModels.GetCurrencyListReponse[]> GetCurrenciesAsync(BuySellClientModels.SupportedFlow flow) =>
		ExecuteWithTimeoutAsync(token =>
		{
			var request = new BuySellClientModels.GetCurrencyListRequest
			{
				CurrencyType = BuySellClientModels.CurrencyType.Fiat,
				SupportedFlow = flow
			};
			return Client.GetCurrencyListAsync(request, token);
		}, CancellationToken.None);

	public Task<BuySellClientModels.GetOffersResponse[]> GetBuyOffersAsync(string currencyFrom, string currencyTo, decimal amount, string countryCode, string? stateCode) =>
		ExecuteWithTimeoutAsync(token =>
		{
			var request = new BuySellClientModels.GetOffersRequest
			{
				CurrencyFrom = currencyFrom,
				CurrencyTo = currencyTo,
				AmountFrom = amount,
				Country = countryCode,
				State = stateCode
			};
			return Client.GetOffersAsync(request, token);
		}, CancellationToken.None);

	public Task<BuySellClientModels.GetSellOffersResponse[]> GetSellOffersAsync(string currencyFrom, string currencyTo, decimal amount, string countryCode, string? stateCode) =>
		ExecuteWithTimeoutAsync(token =>
		{
			var request = new BuySellClientModels.GetSellOffersRequest
			{
				CurrencyFrom = currencyFrom,
				CurrencyTo = currencyTo,
				AmountFrom = amount,
				Country = countryCode,
				State = stateCode
			};
			return Client.GetSellOffersAsync(request, token);
		}, CancellationToken.None);

	public Task<BuySellClientModels.CreateOrderResponse> CreateBuyOrderAsync(
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

			_buySellWalletDataLock.EnterWriteLock();
			try
			{
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
			}
			finally
			{
				_buySellWalletDataLock.ExitWriteLock();
			}

			return response;
		}, CancellationToken.None);

	public Task<BuySellClientModels.CreateSellOrderResponse> CreateSellOrderAsync(
		Wallet wallet,
		string providerCode,
		string currencyTo,
		decimal amountFrom,
		string countryCode,
		string? stateCode,
		string walletAddress,
		string paymentMethod) =>
		ExecuteWithTimeoutAsync(token =>
		{
			var request = new BuySellClientModels.CreateSellOrderRequest
			{
				ProviderCode = providerCode,
				CurrencyFrom = "BTC",
				CurrencyTo = currencyTo,
				AmountFrom = amountFrom,
				Country = countryCode,
				State = stateCode,
				RefundAddress = walletAddress,
				PaymentMethod = paymentMethod,
				ExternalUserId = Guid.NewGuid().ToString(),
				ExternalOrderId = Guid.NewGuid().ToString()
			};

			Task<BuySellClientModels.CreateSellOrderResponse> response = Client.CreateSellOrderAsync(request, token);

			List<BuySellClientModels.GetOrderResponseItem> updatedOrderList = new(wallet.KeyManager.Attributes.BuySellWalletData.Orders);
			var newOrder = new BuySellClientModels.GetOrderResponseItem()
			{
				OrderId = response.Result.OrderId,
				ProviderCode = response.Result.ProviderCode,
				CurrencyFrom = response.Result.CurrencyFrom,
				CurrencyTo = response.Result.CurrencyTo,
				Country = response.Result.Country,
				State = response.Result.State,
				RefundAddress = response.Result.RefundAddress,
				PaymentMethod = response.Result.PaymentMethod,
				Status = BuySellClientModels.OrderStatus.Created,
				RedirectUrl = response.Result.RedirectUrl,
				OrderType = BuySellClientModels.SupportedFlow.Sell,
				UpdatedAt = DateTimeOffset.MinValue,
				CreatedAt = response.Result.CreatedAt,
				AmountFrom = response.Result.AmountFrom
			};

			updatedOrderList.Add(newOrder);

			_buySellWalletDataLock.EnterWriteLock();
			try
			{
				wallet.KeyManager.Attributes.BuySellWalletData.Orders = updatedOrderList.ToArray();
				wallet.KeyManager.ToFile();
			}
			finally
			{
				_buySellWalletDataLock.ExitWriteLock();
			}

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

	public Task<(decimal, decimal)> GetBuyLimitsAsync(string currencyFrom, string countryCode, string? stateCode) =>
		ExecuteWithTimeoutAsync(async token =>
		{
			var request = new BuySellClientModels.GetLimitsRequest
			{
				CurrencyFrom = currencyFrom,
				CurrencyTo = "BTC",
				Country = countryCode,
				State = stateCode
			};

			var result = await Client.GetOrderLimitsAsync(request, token).ConfigureAwait(false);
			var min = result.Select(x => x.Min ?? decimal.MaxValue).Min();
			var max = result.Select(x => x.Max ?? decimal.MinValue).Max();

			return (min, max);
		}, CancellationToken.None);

	public Task<(decimal, decimal)> GetSellLimitsAsync(string currencyTo, string countryCode, string? stateCode) =>
		ExecuteWithTimeoutAsync(async token =>
		{
			var request = new BuySellClientModels.GetLimitsRequest
			{
				CurrencyFrom = "BTC",
				CurrencyTo = currencyTo,
				Country = countryCode,
				State = stateCode
			};

			var result = await Client.GetSellOrderLimitsAsync(request, token).ConfigureAwait(false);
			var min = result.Select(x => x.Min ?? decimal.MaxValue).Min();
			var max = result.Select(x => x.Max ?? decimal.MinValue).Max();

			return (min, max);
		}, CancellationToken.None);

	public BuySellClientModels.GetOrderResponseItem[] GetOrders(Wallet wallet)
	{
		_buySellWalletDataLock.EnterReadLock();
		try
		{
			return wallet.KeyManager.Attributes.BuySellWalletData.Orders.ToArray();
		}
		finally
		{
			_buySellWalletDataLock.ExitReadLock();
		}
	}

	public override void Dispose()
	{
		_modelCancelTokenSource.Cancel();
		_modelCancelTokenSource.Dispose();
	}
}
