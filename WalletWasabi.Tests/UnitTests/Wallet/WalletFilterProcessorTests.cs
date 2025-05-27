using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet;

/// <summary>
/// Tests for <see cref="WalletFilterProcessor"/>.
/// </summary>
public class WalletFilterProcessorTests
{
	private readonly Channel<NewFiltersEventTask> _eventChannel = Channel.CreateUnbounded<NewFiltersEventTask>();

	[Fact]
	public async Task TestFilterProcessingAsync()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(5));

		var node = await MockNode.CreateNodeAsync();
		var wallet = new TestWallet("wallet", node.Rpc);
		await using var builder = new WalletBuilder(node);

		await node.GenerateBlockAsync(testDeadlineCts.Token);

		foreach (var _ in Enumerable.Range(0, 1001))
		{
			await node.GenerateBlockAsync(testDeadlineCts.Token);
		}

		var allFilters = node.BuildFilters().ToList();

		// The MinGapLimit will generate some keys
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet, 2000);

		// Process all but the last 4 which will be processed through events during the synchronization.
		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(allFilters.Take(allFilters.Count - 4).Where(x => x.Header.Height > 101));

		realWallet.BitcoinStore.IndexStore.NewFilters += (_, filters) => Wallet_NewFiltersEmulator(realWallet.WalletFilterProcessor);

		// Mock the database
		foreach (var filter in allFilters)
		{
			realWallet.WalletFilterProcessor.FilterIterator.Cache[filter.Header.Height] = filter;
		}

		await realWallet.WalletFilterProcessor.StartAsync(testDeadlineCts.Token);

		List<Task> allTasks = new();

		// This emulates the synchronization
		var firstTask = Task.Run(async () => await realWallet.WalletFilterProcessor.ProcessAsync(testDeadlineCts.Token), testDeadlineCts.Token);
		allTasks.Add(firstTask);

		// This emulates receiving some new filters while first synchronization is being processed.
		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(new List<FilterModel>() { allFilters.ElementAt(allFilters.Count - 4) });
		var firstExtraFilter = await _eventChannel.Reader.ReadAsync(testDeadlineCts.Token);
		allTasks.Add(firstExtraFilter.Task);

		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(new List<FilterModel>() { allFilters.ElementAt(allFilters.Count - 3) });
		var secondExtraFilter = await _eventChannel.Reader.ReadAsync(testDeadlineCts.Token);
		allTasks.Add(secondExtraFilter.Task);

		// Sync should take some time.
		Assert.False(firstTask.IsCompleted);

		await firstTask;

		// This emulates final synchronization
		var secondTask = Task.Run(async () => await realWallet.WalletFilterProcessor.ProcessAsync(testDeadlineCts.Token), testDeadlineCts.Token);
		allTasks.Add(secondTask);

		// This emulates receiving some new filters while final synchronization is being processed.
		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(new List<FilterModel>() { allFilters.ElementAt(allFilters.Count - 2) });
		var thirdExtraFilter = await _eventChannel.Reader.ReadAsync(testDeadlineCts.Token);
		allTasks.Add(thirdExtraFilter.Task);

		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(new List<FilterModel>() { allFilters.ElementAt(allFilters.Count - 1) });
		var fourthExtraFilter = await _eventChannel.Reader.ReadAsync(testDeadlineCts.Token);
		allTasks.Add(fourthExtraFilter.Task);

		var whenAll = Task.WhenAll(allTasks);
		// All tasks should finish
		await whenAll;

		// Blockchain Tip should be reached
		Assert.Equal(realWallet.BitcoinStore.SmartHeaderChain.TipHeight, (uint)realWallet.KeyManager.GetBestHeight().Value);
	}

	// This emulates the NewFiltersProcessed event
	private void Wallet_NewFiltersEmulator(WalletFilterProcessor walletFilterProcessor)
	{
		// Initiate tasks and write tasks to the channel to pass them back to the test.
		// Underlying tasks without cancellation token as it works on Wallet.
		Task task = walletFilterProcessor.ProcessAsync(CancellationToken.None);
		_eventChannel.Writer.TryWrite(new NewFiltersEventTask(task));
	}

	private record NewFiltersEventTask(Task Task);
}
