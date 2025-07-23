using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.Tor.StatusChecker.ApiModels;

namespace WalletWasabi.Tor.StatusChecker;

/// <summary>
/// Component that periodically checks https://status.torproject.org/ to detect network disruptions.
/// </summary>
public class TorStatusChecker : PeriodicRunner
{
	private static readonly Uri TorStatusUri = new("https://status.torproject.org/index.json");

	public TorStatusChecker(TimeSpan period, IHttpClient httpClient) : base(period)
	{
		HttpClient = httpClient;
	}

	public event EventHandler<Issue[]>? StatusEvent;

	private IHttpClient HttpClient { get; }

	/// <inheritdoc/>
	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		try
		{
			using HttpRequestMessage request = new(HttpMethod.Get, TorStatusUri);
			using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

			var torNetworkStatus = await response.Content.ReadAsJsonAsync<TorNetworkStatus>().ConfigureAwait(false);

			var issues =
				torNetworkStatus.Systems
					.Where(x => x.Category == "Tor Network")
					.Where(x => x.UnresolvedIssues.Count != 0)
					.SelectMany(x => x.UnresolvedIssues)
					.Select(x => new Issue(x.Title, x.Resolved))
					.ToArray();

			// Fire event.
			StatusEvent?.Invoke(this, issues);
		}
		catch (Exception ex)
		{
			Logger.LogDebug("Failed to get/parse Tor status page.", ex);
		}
	}
}
