using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.History.ViewModels.HistoryItems;
using WalletWasabi.Fluent.HomeScreen.Wallets.ViewModels;
using WalletWasabi.Fluent.SearchBar.Interfaces;
using WalletWasabi.Fluent.SearchBar.Models;
using WalletWasabi.Fluent.SearchBar.ViewModels.SearchItems;
using WalletWasabi.Helpers;
using WalletWasabi.Lang;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.SearchBar.ViewModels.Sources;

public class TransactionsSearchSource : ReactiveObject, ISearchSource, IDisposable
{
	private const int MaxResultCount = 5;
	private const int MinQueryLength = 3;

	private readonly CompositeDisposable _disposables = new();

	public TransactionsSearchSource(IObservable<string> queries)
	{
#pragma warning disable CA2000 // Dispose objects before losing scope
		var sourceCache = new SourceCache<ISearchItem, ComposedKey>(x => x.Key)
			.DisposeWith(_disposables);
#pragma warning restore CA2000 // Dispose objects before losing scope

		var results = queries
			.Throttle(TimeSpan.FromMilliseconds(180))                  // rate-limit while typing
			.DistinctUntilChanged(StringComparer.Ordinal)
			.Select(q =>
				string.IsNullOrWhiteSpace(q) || q.Length < MinQueryLength
					? Observable.Return(Enumerable.Empty<ISearchItem>())
					: Observable.Start(() => Search(q).ToList(), RxApp.TaskpoolScheduler)) // heavy work off UI thread
			.Switch()                                                  // cancel stale searches
			.ObserveOn(RxApp.MainThreadScheduler);                     // update cache on UI thread

		sourceCache
			.RefillFrom(results)
			.DisposeWith(_disposables);

		Changes = sourceCache.Connect();
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}

	public IObservable<IChangeSet<ISearchItem, ComposedKey>> Changes { get; }

	private static bool ContainsId(HistoryItemViewModelBase historyItemViewModelBase, string queryStr)
	{
		return historyItemViewModelBase.Transaction.Id.ToString()
			.Contains(queryStr, StringComparison.OrdinalIgnoreCase);
	}

	private static Task NavigateTo(WalletViewModel wallet, HistoryItemViewModelBase item)
	{
		var walletPageViewModel = MainViewModel.Instance.NavBar.Wallets.FirstOrDefault(x => x.WalletViewModel == wallet);
		if (walletPageViewModel == MainViewModel.Instance.NavBar.SelectedWallet)
		{
			wallet.SelectTransaction(item.Transaction.Id);
		}
		else
		{
			MainViewModel.Instance.NavBar.SelectedWallet = walletPageViewModel;
			wallet.NavigateAndHighlight(item.Transaction.Id);
		}

		return Task.CompletedTask;
	}

	private static string GetIcon(HistoryItemViewModelBase historyItemViewModelBase)
	{
		return historyItemViewModelBase switch
		{
			CoinJoinHistoryItemViewModel => "shield_regular",
			CoinJoinsHistoryItemViewModel => "double_shield_regular",
			TransactionHistoryItemViewModel => "normal_transaction",
			_ => ""
		};
	}

	private static IEnumerable<(WalletViewModel, HistoryItemViewModelBase)> Flatten(IEnumerable<(WalletViewModel Wallet, IEnumerable<HistoryItemViewModelBase> Transactions)> walletTransactions)
	{
		return walletTransactions.SelectMany(t => t.Transactions.Select(item => (t.Wallet, HistoryItem: item)));
	}

	private static IEnumerable<(WalletViewModel Wallet, IEnumerable<HistoryItemViewModelBase> Transactions)> GetTransactionsByWallet()
	{
		// TODO: This is a workaround to get all the transactions from currently loaded wallets. REMOVE after UIDecoupling #26

		return MainViewModel.Instance.NavBar.Wallets
			.Where(x => x.IsLoggedIn && x.Wallet.State == WalletState.Started)
			.Select(x => x.WalletViewModel)
			.WhereNotNull()
			.Select(x => (Wallet: x,
				x.History.Transactions.Concat(x.History.Transactions.OfType<CoinJoinsHistoryItemViewModel>().SelectMany(y => y.Children))));
	}

	private static List<(WalletViewModel Wallet, HistoryItemViewModelBase Item)> SnapshotTransactions()
	{
		// materialize once per query to avoid repeated enumeration / UI access
		return Flatten(GetTransactionsByWallet()).ToList();
	}

	private static IEnumerable<ISearchItem> Search(string query)
	{
		var snapshot = SnapshotTransactions();

		if (!snapshot.Any())
		{
			return Enumerable.Empty<ISearchItem>();
		}

		// cache destination addresses per tx within a single search
		var destCache = new Dictionary<uint256, IReadOnlyCollection<BitcoinAddress>>();

		var results = new List<ISearchItem>(Math.Min(MaxResultCount, 16));

		// parse address at most once per wallet/network
		foreach (var group in snapshot.GroupBy(t => t.Wallet))
		{
			BitcoinAddress? parsedAddr = null;
			if (NBitcoinHelpers.TryParseBitcoinAddress(group.Key.WalletModel.Network, query, out var addr))
			{
				parsedAddr = addr;
			}

			foreach (var (wallet, item) in group)
			{
				bool isMatch;
				if (parsedAddr is null)
				{
					isMatch = ContainsId(item, query);
				}
				else
				{
					var txid = item.Transaction.Id;
					if (!destCache.TryGetValue(txid, out var dests))
					{
						dests = wallet.WalletModel.Transactions.GetDestinationAddresses(txid).ToList();
						destCache[txid] = dests;
					}
					isMatch = dests.Contains(parsedAddr);
				}

				if (isMatch)
				{
					results.Add(ToSearchItem(wallet, item));
					if (results.Count >= MaxResultCount)
					{
						return results;
					}
				}
			}
		}

		return results;
	}

	private static ISearchItem ToSearchItem(WalletViewModel wallet, HistoryItemViewModelBase item)
	{
		return new ActionableItem(
			item.Transaction.Id.ToString(),
			Resources.FoundIn.SafeInject(wallet.WalletModel.Name),
			() => NavigateTo(wallet, item),
			Resources.WalletTransactions,
			new List<string>())
		{
			Icon = GetIcon(item)
		};
	}
}
