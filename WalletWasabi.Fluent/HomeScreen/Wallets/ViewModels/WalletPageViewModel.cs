using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.HomeScreen.Loading.ViewModels;
using WalletWasabi.Fluent.Login.ViewModels;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Navigation.Models;
using WalletWasabi.Fluent.Navigation.ViewModels;
using WalletWasabi.Services;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.HomeScreen.Wallets.ViewModels;

public partial class WalletPageViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	[AutoNotify] private bool _isLoggedIn;
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private bool _isLoading;
	[AutoNotify] private string? _iconName;
	[AutoNotify] private string? _iconNameFocused;
	[AutoNotify] private WalletViewModel? _walletViewModel;
	[AutoNotify] private RoutableViewModel? _currentPage;
	[AutoNotify] private string? _title;

	public WalletPageViewModel(WalletModel walletModel)
	{
		WalletModel = walletModel;

		// TODO: Finish partial refactor
		// Wallet property must be removed
		Wallet = Services.WalletManager.GetWalletByName(walletModel.Name);

		// Show Login Page when wallet is not logged in
		this.WhenAnyValue(x => x.IsLoggedIn)
			.Where(x => !x)
			.Do(_ => ShowLogin())
			.Subscribe()
			.DisposeWith(_disposables);

		// Show wallet home screen
		this.WhenAnyValue(x => x.IsLoggedIn)
			.Where(x => x)
			.Do(_ => ShowWallet())
			.Subscribe()
			.DisposeWith(_disposables);

		this.WhenAnyValue(x => x.WalletModel.Auth.IsLoggedIn)
			.BindTo(this, x => x.IsLoggedIn)
			.DisposeWith(_disposables);

		// Navigate to current page when IsSelected and CurrentPage change
		this.WhenAnyValue(x => x.IsSelected, x => x.CurrentPage)
			.Where(t => t.Item1)
			.Select(t => t.Item2)
			.WhereNotNull()
			.Do(x => UiContext.Navigate().Navigate(NavigationTarget.HomeScreen).To(x, NavigationMode.Clear))
			.Subscribe()
			.DisposeWith(_disposables);

		this.WhenAnyValue(x => x.WalletModel.Name)
			.BindTo(this, x => x.Title)
			.DisposeWith(_disposables);

		this.WhenAnyValue(x => x.IsSelected)
			.Do(value => WalletModel.IsSelected = value)
			.Subscribe();

		SetIcon();
	}

	public WalletModel WalletModel { get; }

	public Wallet Wallet { get; }

	private void ShowLogin()
	{
		CurrentPage = new LoginViewModel(WalletModel);
	}

	private void ShowWallet()
	{
		WalletViewModel =
			WalletModel.IsHardwareWallet
			? new HardwareWalletViewModel(WalletModel, Wallet)
			: new WalletViewModel(WalletModel, Wallet);

		this.WhenAnyValue(x => x.WalletModel.Loader.IsLoading)
			.BindTo(this, x => x.IsLoading)
			.DisposeWith(_disposables);

		// Pass IsSelected down to WalletViewModel.IsSelected
		this.WhenAnyValue(x => x.IsSelected)
			.BindTo(WalletViewModel, x => x.IsSelected)
			.DisposeWith(_disposables);

		CurrentPage = WalletViewModel;
	}

	public void Dispose() => _disposables.Dispose();

	private void SetIcon()
	{
		var walletType = WalletModel.Settings.WalletType;

		var baseResourceName = walletType switch
		{
			WalletType.Coldcard => "coldcard_24",
			WalletType.Trezor => "trezor_24",
			WalletType.Ledger => "ledger_24",
			WalletType.BitBox => "bitbox_24",
			WalletType.Jade => "jade_24",
			_ => "wallet_24"
		};

		IconName = $"nav_{baseResourceName}_regular";
		IconNameFocused = $"nav_{baseResourceName}_filled";
	}
}
