using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.HomeScreen.Labels.Models;
using WalletWasabi.Fluent.HomeScreen.Labels.ViewModels;
using WalletWasabi.Fluent.HomeScreen.Send.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.Send.ViewModels;

public partial class RecipientRowViewModel : ViewModelBase, IDisposable
{
	private readonly Network _network;
	private readonly CompositeDisposable _disposables = new();
	[AutoNotify] private string _to = "";
	[AutoNotify] private decimal? _amountBtc;
	[AutoNotify] private string? _error;

	public RecipientRowViewModel(WalletModel wallet, Action<RecipientRowViewModel, string?> changed,
		Action<RecipientRowViewModel> remove, Action<RecipientRowViewModel> insertMax, LabelsArray? labels = null)
	{
		_network = wallet.Network;
		_exchangeRate = wallet.AmountProvider.ExchangeRate;
		wallet.AmountProvider.ExchangeRateObservable.ObserveOn(RxApp.MainThreadScheduler)
			.BindTo(this, x => x.ExchangeRate).DisposeWith(_disposables);
		FiatTicker = Resources.Culture.GetFiatTicker();
		SuggestionLabels = new SuggestionLabelsViewModel(wallet, Intent.Send, 3, labels);
		SuggestionLabels.Activate(_disposables);
		RemoveCommand = ReactiveCommand.Create(() => remove(this));
		InsertMaxCommand = ReactiveCommand.Create(() => insertMax(this));
		PasteCommand = ReactiveCommand.CreateFromTask(async () => To = (await ApplicationHelper.GetTextAsync())?.Trim() ?? "");
		QrCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			var address = await UiContext.Navigate().To().ShowQrCameraDialog(wallet.Network).GetResultAsync();
			if (!string.IsNullOrWhiteSpace(address))
			{
				To = address;
			}
		});
		this.Changed.Where(x => x.PropertyName is nameof(To) or nameof(AmountBtc))
			.Subscribe(x => changed(this, x.PropertyName)).DisposeWith(_disposables);
		SuggestionLabels.WhenAnyValue(x => x.IsCurrentTextValid)
			.Subscribe(_ => changed(this, null)).DisposeWith(_disposables);
	}

	[AutoNotify] private decimal _exchangeRate;
	public string FiatTicker { get; }
	public SuggestionLabelsViewModel SuggestionLabels { get; }
	public ICommand RemoveCommand { get; }
	public ICommand InsertMaxCommand { get; }
	public ICommand PasteCommand { get; }
	public ICommand QrCommand { get; }
	public bool IsQrButtonVisible => UiContext.QrCodeReader.IsPlatformSupported;

	public bool TryGetRecipient(out RecipientInfo? recipient)
	{
		var valid = RecipientInfo.TryCreate(To, AmountBtc, new LabelsArray(SuggestionLabels.Labels.ToArray()), _network,
			out recipient, out var error);
		Error = error;
		return valid;
	}

	public void Dispose() => _disposables.Dispose();
}
