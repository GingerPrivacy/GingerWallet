using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using AddressFunc = System.Func<WalletWasabi.Fluent.Models.Wallets.AddressModel, System.Threading.Tasks.Task>;
using AddressAction = System.Action<WalletWasabi.Fluent.Models.Wallets.AddressModel>;

namespace WalletWasabi.Fluent.HomeScreen.Receive.ViewModels;

public partial class AddressViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	[AutoNotify] private string _addressText;
	[AutoNotify] private LabelsArray _labels;
	[AutoNotify] private string _type;
	[AutoNotify] private string _shortType;

	public AddressViewModel(UiContext context, AddressFunc onEdit, AddressAction onShow, AddressModel addressModel)
	{
		UiContext = context;
		AddressModel = addressModel;
		_addressText = addressModel.Text;

		this.WhenAnyValue(x => x.AddressModel.Labels)
			.BindTo(this, viewModel => viewModel.Labels)
			.DisposeWith(_disposables);

		_type = AddressModel.Type.Name;
		_shortType = AddressModel.Type.ShortName;

		CopyAddressCommand = ReactiveCommand.CreateFromTask(() => UiContext.Clipboard.SetTextAsync(AddressText));
		HideAddressCommand = ReactiveCommand.CreateFromTask(PromptHideAddressAsync);
		EditLabelCommand = ReactiveCommand.CreateFromTask(() => onEdit(addressModel));
		NavigateCommand = ReactiveCommand.Create(() => onShow(addressModel));
	}

	private AddressModel AddressModel { get; }

	public ICommand CopyAddressCommand { get; }

	public ICommand HideAddressCommand { get; }

	public ICommand EditLabelCommand { get; }

	public ReactiveCommand<Unit, Unit> NavigateCommand { get; }

	private async Task PromptHideAddressAsync()
	{
		var result = await UiContext.Navigate().To().ConfirmHideAddress(AddressModel.Labels).GetResultAsync();
		if (result == false)
		{
			return;
		}

		AddressModel.Hide();

		var isAddressCopied = await UiContext.Clipboard.GetTextAsync() == AddressModel.Text;

		if (isAddressCopied)
		{
			await UiContext.Clipboard.ClearAsync();
		}
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
