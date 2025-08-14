using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;
using WalletWasabi.WebClients.Wasabi;
using static WalletWasabi.BuySell.BuySellClientModels;

public class BuySellClient
{
	private static readonly JsonSerializerOptions JsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

	public BuySellClient(WasabiHttpClientFactory httpClientFactory)
	{
		HttpClient = httpClientFactory.NewHttpClient(
			httpClientFactory.BackendUriGetter,
			WalletWasabi.Tor.Socks5.Pool.Circuits.Mode.NewCircuitPerRequest);
	}

	private IHttpClient HttpClient { get; }

	/// <summary>
	/// Sends an HTTP request to the specified endpoint with an optional payload,
	/// then deserializes the JSON response into the given type.
	/// </summary>
	private async Task<TResponse> SendRequestAsync<TResponse>(
		string endpoint,
		HttpMethod method,
		object? payload,
		CancellationToken cancellationToken)
	{
		HttpResponseMessage response;

		if (payload is not null)
		{
			string json = JsonSerializer.Serialize(payload);
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			response = await HttpClient.SendAsync(method, endpoint, content, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			response = await HttpClient.SendAsync(method, endpoint, cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		using (response)
		{
			response.EnsureSuccessStatusCode();
			var contentJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			var responseModel = JsonSerializer.Deserialize<TResponse>(contentJson, JsonSerializerOptions);
			if (responseModel is null)
			{
				throw new HttpRequestException("Failed to deserialize response.");
			}
			return responseModel;
		}
	}

	public Task<GetOffersResponse[]> GetOffersAsync(GetOffersRequest getOffersRequest, CancellationToken cancellationToken) =>
		SendRequestAsync<GetOffersResponse[]>("buysell/offers", HttpMethod.Post, getOffersRequest, cancellationToken);

	public Task<GetAvailableCountriesResponse> GetAvailableCountriesAsync(GetAvailableCountriesRequest getAvailableCountriesRequest, CancellationToken cancellationToken) =>
		SendRequestAsync<GetAvailableCountriesResponse>("buysell/available-countries", HttpMethod.Post, getAvailableCountriesRequest, cancellationToken);

	public Task<GetCurrencyListReponse[]> GetCurrencyListAsync(GetCurrencyListRequest getCurrencyListRequest, CancellationToken cancellationToken) =>
		SendRequestAsync<GetCurrencyListReponse[]>("buysell/currencies", HttpMethod.Post, getCurrencyListRequest, cancellationToken);

	public Task<GetProvidersListReponse[]> GetProvidersListAsync(CancellationToken cancellationToken) =>
		SendRequestAsync<GetProvidersListReponse[]>("buysell/providers", HttpMethod.Post, payload: null, cancellationToken);

	public Task<ValidateWalletAddressResponse> ValidateAddressAsync(ValidateWalletAddressRequest validateWalletAddressRequest, CancellationToken cancellationToken) =>
		SendRequestAsync<ValidateWalletAddressResponse>("buysell/validate-address", HttpMethod.Post, validateWalletAddressRequest, cancellationToken);

	public Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest createOrderRequest, CancellationToken cancellationToken) =>
		SendRequestAsync<CreateOrderResponse>("buysell/orders", HttpMethod.Post, createOrderRequest, cancellationToken);

	public Task<GetOrderResponse> GetOrderAsync(GetOrderRequest getOrderRequest, CancellationToken cancellationToken) =>
		SendRequestAsync<GetOrderResponse>("buysell/get-orders", HttpMethod.Post, getOrderRequest, cancellationToken);

	public Task<GetLimitsResponse[]> GetOrderLimitsAsync(GetLimitsRequest getLimitsRequest, CancellationToken cancellationToken) =>
		SendRequestAsync<GetLimitsResponse[]>("buysell/get-limits", HttpMethod.Post, getLimitsRequest, cancellationToken);

	public Task<GetSellOffersResponse[]> GetSellOffersAsync(GetSellOffersRequest getSellOffersRequest, CancellationToken cancellationToken) =>
		SendRequestAsync<GetSellOffersResponse[]>("buysell/sell-offers", HttpMethod.Post, getSellOffersRequest, cancellationToken);

	public Task<GetLimitsResponse[]> GetSellOrderLimitsAsync(GetLimitsRequest getSellLimitsRequest, CancellationToken cancellationToken) =>
		SendRequestAsync<GetLimitsResponse[]>("buysell/sell-limits", HttpMethod.Post, getSellLimitsRequest, cancellationToken);

	public Task<CreateSellOrderResponse> CreateSellOrderAsync(CreateSellOrderRequest createSellOrderRequest, CancellationToken cancellationToken) =>
		SendRequestAsync<CreateSellOrderResponse>("buysell/sell-orders", HttpMethod.Post, createSellOrderRequest, cancellationToken);
}
