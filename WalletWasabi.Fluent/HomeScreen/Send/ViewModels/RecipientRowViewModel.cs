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
	[AutoNotify] private bool _canRemove;

	public RecipientRowViewModel(WalletModel wallet, Action changed, Action<RecipientRowViewModel> remove, Action<RecipientRowViewModel> insertMax)
	{
		_network = wallet.Network;
		_exchangeRate = wallet.AmountProvider.ExchangeRate;
		wallet.AmountProvider.ExchangeRateObservable.ObserveOn(RxApp.MainThreadScheduler)
			.BindTo(this, x => x.ExchangeRate).DisposeWith(_disposables);
		FiatTicker = Resources.Culture.GetFiatTicker();
		SuggestionLabels = new SuggestionLabelsViewModel(wallet, Intent.Send, 3);
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
		this.WhenAnyValue(x => x.To, x => x.AmountBtc, x => x.SuggestionLabels.IsCurrentTextValid)
			.Subscribe(_ => changed()).DisposeWith(_disposables);
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
