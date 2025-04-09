using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Fluent.HomeScreen.Labels.Models;
using WalletWasabi.Fluent.HomeScreen.Labels.ViewModels;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.Receive.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class AddressLabelEditViewModel : DialogViewModelBase<LabelsArray?>
{
	[AutoNotify] private bool _isCurrentTextValid;

	public AddressLabelEditViewModel(IWalletModel wallet, IAddress address)
	{
		Title = Resources.EditLabels;
		SuggestionLabels = new SuggestionLabelsViewModel(wallet, Intent.Receive, 3, address.Labels);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		var canExecute =
			this.WhenAnyValue(x => x.SuggestionLabels.Labels.Count, x => x.IsCurrentTextValid)
				.Select(tup =>
				{
					var (labelsCount, isCurrentTextValid) = tup;
					return labelsCount > 0 || isCurrentTextValid;
				});

		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, new LabelsArray(SuggestionLabels.Labels)), canExecute);
	}

	public SuggestionLabelsViewModel SuggestionLabels { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		SuggestionLabels.Activate(disposables);
	}
}
