using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.FilterProcessor;
using WalletWasabi.WebClients.Wasabi;
using WalletWasabi.Daemon.FeeRateProviders;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public class WalletBuilder : IAsyncDisposable
{
	public WalletBuilder(MockNode node, [CallerMemberName] string callerName = "NN")
	{
		DataDir = TestDirectory.Get(nameof(WalletSynchronizationTests), callerName);

		SmartHeaderChain smartHeaderChain = new();
		IndexStore = new IndexStore(Path.Combine(DataDir, "indexStore"), node.Network, smartHeaderChain);
		TransactionStore = new AllTransactionStore(Path.Combine(DataDir, "transactionStore"), node.Network);

		Filters = node.BuildFilters();

		var blockRepositoryMock = new MockFileSystemBlockRepository(node.BlockChain);
		BitcoinStore = new BitcoinStore(IndexStore, TransactionStore, new MempoolService(), smartHeaderChain, blockRepositoryMock);
		Cache = new MemoryCache(new MemoryCacheOptions());
		HttpClientFactory = new WasabiHttpClientFactory(torEndPoint: null, backendUriGetter: () => null!);
		Synchronizer = new(period: TimeSpan.FromSeconds(3), 1000, BitcoinStore, HttpClientFactory);
		BlockDownloadService = new(BitcoinStore.BlockRepository, trustedFullNodeBlockProviders: [], p2pBlockProvider: null);
		UnconfirmedTransactionChainProvider = new(HttpClientFactory);
	}

	private IndexStore IndexStore { get; }
	private AllTransactionStore TransactionStore { get; }
	private BitcoinStore BitcoinStore { get; }
	private MemoryCache Cache { get; }
	private WasabiHttpClientFactory HttpClientFactory { get; }
	private WasabiSynchronizer Synchronizer { get; }
	private BlockDownloadService BlockDownloadService { get; }
	private UnconfirmedTransactionChainProvider UnconfirmedTransactionChainProvider { get; }
	public IEnumerable<FilterModel> Filters { get; }
	public string DataDir { get; }

	public async Task<WalletWasabi.Wallets.Wallet> CreateRealWalletBasedOnTestWalletAsync(TestWallet wallet, int? minGapLimit = null)
	{
		await BlockDownloadService.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await BitcoinStore.InitializeAsync().ConfigureAwait(false); // StartingFilter already added to IndexStore after this line.

		await BitcoinStore.IndexStore.AddNewFiltersAsync(Filters.Skip(1)).ConfigureAwait(false);
		var keyManager = KeyManager.CreateNewWatchOnly(wallet.GetSegwitAccountExtPubKey(), null!, null, minGapLimit);
		keyManager.GetKeys(_ => true); // Make sure keys are asserted.

		var serviceConfiguration = new ServiceConfiguration(new UriEndPoint(new Uri("http://www.nomatter.dontcare")), Money.Coins(WalletWasabi.Helpers.Constants.DefaultDustThreshold));

		var feeProvider = new FeeRateProvider(HttpClientFactory, keyManager.GetNetwork());

		WalletFactory walletFactory = new(DataDir, Network.RegTest, BitcoinStore, Synchronizer, serviceConfiguration, feeProvider, BlockDownloadService, UnconfirmedTransactionChainProvider);
		return walletFactory.CreateAndInitialize(keyManager);
	}

	public async ValueTask DisposeAsync()
	{
		await IndexStore.DisposeAsync().ConfigureAwait(false);
		await Synchronizer.StopAsync(CancellationToken.None).ConfigureAwait(false);
		await TransactionStore.DisposeAsync().ConfigureAwait(false);
		await HttpClientFactory.DisposeAsync().ConfigureAwait(false);
		BlockDownloadService.Dispose();
		UnconfirmedTransactionChainProvider.Dispose();
		Cache.Dispose();
	}
}
