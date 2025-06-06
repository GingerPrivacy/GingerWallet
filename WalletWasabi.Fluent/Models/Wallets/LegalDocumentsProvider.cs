using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Legal;

namespace WalletWasabi.Fluent.Models.Wallets;

public class LegalDocumentsProvider
{
	public Task<LegalDocuments> WaitAndGetLatestDocumentAsync()
	{
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
		return Services.LegalChecker.WaitAndGetLatestDocumentAsync(timeout.Token);
	}
}
