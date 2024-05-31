using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace WalletWasabi.Tests.UnitTests;

public class MockHttpClientResponse : MockHttpClient
{
	public MockHttpClientResponse()
	{
		OnSendAsync = OnSendResponseAsync;
		StatusCode = HttpStatusCode.OK;
		Content = "";
	}

	public void SetResponse(HttpStatusCode statusCode, string content)
	{
		StatusCode = statusCode;
		Content = content;
	}

	public HttpStatusCode StatusCode { get; set; }
	public string Content { get; set; }

	private Task<HttpResponseMessage> OnSendResponseAsync(HttpRequestMessage request)
	{
		HttpResponseMessage response = new(StatusCode);
		response.Content = new StringContent(Content);
		return Task.FromResult(response);
	}
}
