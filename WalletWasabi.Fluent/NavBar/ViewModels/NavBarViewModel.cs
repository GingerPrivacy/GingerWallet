using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.HomeScreen.Wallets.ViewModels;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.ViewModels;

namespace WalletWasabi.Fluent.NavBar.ViewModels;

/// <summary>
/// The ViewModel that represents the structure of the sidebar.
/// </summary>
[AppLifetime]
public partial class NavBarViewModel : ViewModelBase
{
	[AutoNotify] private WalletPageViewModel? _selectedWallet;
	[AutoNotify] private WalletModel? _selectedWalletModel;

	public NavBarViewModel()
	{
		BottomItems = new ObservableCollection<NavBarItemViewModel>();

		UiContext.WalletRepository
				 .Wallets
				 .Connect()
				 .Transform(newWallet => new WalletPageViewModel(newWallet))
				 .DisposeMany()
				 .AutoRefresh(x => x.IsLoggedIn)
				 .Sort(SortExpressionComparer<WalletPageViewModel>.Descending(i => i.IsLoggedIn).ThenByAscending(x => x.WalletModel.Name))
				 .Bind(out var wallets)
				 .Subscribe();

		wallets
			.ToObservableChangeSet()
			.Subscribe(_ =>
			{
				if (Wallets is not null && SelectedWallet is not null && !Wallets.Contains(SelectedWallet))
				{
					SelectedWallet = Wallets.FirstOrDefault();
				}
			});

		Wallets = wallets;
	}

	public ObservableCollection<NavBarItemViewModel> BottomItems { get; }

	public ReadOnlyObservableCollection<WalletPageViewModel> Wallets { get; }

	public void Activate()
	{
		this.WhenAnyValue(x => x.SelectedWallet)
			.Buffer(2, 1)
			.Select(buffer => (OldValue: buffer[0], NewValue: buffer[1]))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(x =>
			{
				if (x.OldValue is { } a)
				{
					a.IsSelected = false;
				}

				if (x.NewValue is { } b)
				{
					b.IsSelected = true;
					UiContext.WalletRepository.StoreLastSelectedWallet(b.WalletModel);
				}
			})
			.Subscribe();

		this.WhenAnyValue(x => x.SelectedWallet!.WalletModel)
			.BindTo(this, x => x.SelectedWalletModel);

		SelectedWallet = Wallets.FirstOrDefault(x => x.WalletModel.Name == UiContext.WalletRepository.DefaultWalletName) ?? Wallets.FirstOrDefault();
	}

	public async Task InitialiseAsync()
	{
		var bottomItems = NavigationManager.MetaData.Where(x => x.NavBarPosition == NavBarPosition.Bottom);

		foreach (var item in bottomItems)
		{
			var viewModel = await NavigationManager.MaterializeViewModelAsync(item);

			if (viewModel is INavBarItem navBarItem)
			{
				BottomItems.Add(new NavBarItemViewModel(navBarItem));
			}
		}
	}

	public WalletViewModel? Select(WalletModel wallet)
	{
		SelectedWallet = Wallets.First(x => x.WalletModel.Name == wallet.Name);
		return SelectedWallet.WalletViewModel;
	}
}
