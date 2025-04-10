using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Net;

namespace GingerCommon.Static;

public static class HttpUtils
{
	public static WebProxy? CreateTorProxy(EndPoint? torEndpoint)
	{
		if (torEndpoint is IPEndPoint ipEndpoint)
		{
			return new WebProxy($"socks5://{ipEndpoint.Address}:{ipEndpoint.Port}");
		}
		if (torEndpoint is DnsEndPoint dnsEndpoint)
		{
			return new WebProxy($"socks5://{dnsEndpoint.Host}:{dnsEndpoint.Port}");
		}
		return null;
	}

	public static async Task<string> HttpGetAsync(string baseAddress, string request, EndPoint? torEndpoint = null, Action<HttpClient>? setup = null, CancellationToken? cancellationToken = null)
	{
		WebProxy? proxy = CreateTorProxy(torEndpoint);
		if (proxy is null)
		{
#pragma warning disable RS0030 // Do not use banned APIs
			using HttpClient httpClient = new();
#pragma warning restore RS0030 // Do not use banned APIs
			setup?.Invoke(httpClient);
			return await HttpGetAsync(httpClient, baseAddress, request, cancellationToken);
		}
		else
		{
			using var httpHandler = new SocketsHttpHandler { Proxy = proxy, UseProxy = true };
#pragma warning disable RS0030 // Do not use banned APIs
			using HttpClient httpClient = new(httpHandler);
#pragma warning restore RS0030 // Do not use banned APIs
			setup?.Invoke(httpClient);
			return await HttpGetAsync(httpClient, baseAddress, request, cancellationToken);
		}
	}

	public static async Task<string> HttpGetAsync(HttpClient httpClient, string baseAddress, string request, CancellationToken? cancellationToken)
	{
		CancellationToken token = cancellationToken ?? CancellationToken.None;

		httpClient.BaseAddress = new Uri(baseAddress);
		using var response = await httpClient.GetAsync(request, token).ConfigureAwait(false);
		var contentString = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
		return contentString;
	}
}
