using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Lang;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.HomeScreen.History.ViewModels.HistoryItems;

public abstract partial class HistoryItemViewModelBase : ViewModelBase, ITreeDataGridExpanderItem
{
	[AutoNotify] private bool _isFlashing;
	[AutoNotify] private bool _isExpanded;
	[AutoNotify] private bool _isPointerOver;
	[AutoNotify] private bool _isParentPointerOver;
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private bool _isParentSelected;

	protected HistoryItemViewModelBase(TransactionModel transaction)
	{
		Transaction = transaction;
		IsChild = transaction.IsChild;
		ClipboardCopyCommand = ReactiveCommand.CreateFromTask<string>(text => UiContext.Clipboard.SetTextAsync(text));
		OpenInBrowserCommand = ReactiveCommand.CreateFromTask(() => OnOpenInBrowserAsync(transaction));
		HasBeenSpedUp = transaction.HasBeenSpedUp;

		this.WhenAnyValue(x => x.IsFlashing)
			.Where(x => x)
			.SubscribeAsync(async _ =>
			{
				await Task.Delay(1800);
				IsFlashing = false;
			});

		this.WhenAnyValue(x => x.IsPointerOver)
			.Do(x =>
			{
				foreach (var child in Children)
				{
					child.IsParentPointerOver = x;
				}
			})
			.Subscribe();

		this.WhenAnyValue(x => x.IsSelected)
			.Do(x =>
			{
				foreach (var child in Children)
				{
					child.IsParentSelected = x;
				}
			})
			.Subscribe();
	}

	/// <summary>
	/// Proxy property to prevent stack overflow due to internal bug in Avalonia where the OneWayToSource Binding
	/// is replaced by a TwoWay one.when
	/// </summary>
	public bool IsPointerOverProxy
	{
		get => IsPointerOver;
		set => IsPointerOver = value;
	}

	public bool IsSelectedProxy
	{
		get => IsSelected;
		set => IsSelected = value;
	}

	public TransactionModel Transaction { get; }

	public ObservableCollection<HistoryItemViewModelBase> Children { get; } = new();

	public bool IsChild { get; set; }

	public bool IsLastChild { get; set; }

	public ICommand? ShowDetailsCommand { get; protected set; }

	public ICommand? ClipboardCopyCommand { get; protected set; }

	public ICommand? SpeedUpTransactionCommand { get; protected set; }

	public bool HasBeenSpedUp { get; set; }

	public bool CanBeSpedUp { get; protected set; }

	public ICommand? CancelTransactionCommand { get; protected set; }
	public ICommand? OpenInBrowserCommand { get; protected set; }
	public bool CanOpenInBrowser { get; init; }

	public bool HasChildren() => Children.Count > 0;

	public static Comparison<HistoryItemViewModelBase?> SortAscending<T>(Func<HistoryItemViewModelBase, T> selector, IComparer<T>? comparer = null)
	{
		return Sort(selector, comparer, reverse: false);
	}

	public static Comparison<HistoryItemViewModelBase?> SortDescending<T>(Func<HistoryItemViewModelBase, T> selector, IComparer<T>? comparer = null)
	{
		return Sort(selector, comparer, reverse: true);
	}

	private static Comparison<HistoryItemViewModelBase?> Sort<T>(Func<HistoryItemViewModelBase, T> selector, IComparer<T>? comparer, bool reverse)
	{
		return (x, y) =>
		{
			var ordering = reverse ? -1 : 1;

			if (x is null && y is null)
			{
				return 0;
			}

			if (x is null)
			{
				return -ordering;
			}

			if (y is null)
			{
				return ordering;
			}

			// Confirmation comparison must be the same for both sort directions..
			var result = x.Transaction.IsConfirmed.CompareTo(y.Transaction.IsConfirmed);
			if (result == 0)
			{
				var xValue = selector(x);
				var yValue = selector(y);

				result =
					comparer?.Compare(xValue, yValue) ??
					Comparer<T>.Default.Compare(xValue, yValue);
				result *= ordering;
			}

			return result;
		};
	}

	private async Task OnOpenInBrowserAsync(TransactionModel transaction)
	{
		try
		{
			string urlToOpen = $"https://mempool.space/tx/{transaction.Id}";

			await WebBrowserService.Instance.OpenUrlInPreferredBrowserAsync(urlToOpen).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), Resources.Browser, Resources.BrowserError);
		}
	}
}
