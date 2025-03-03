using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Affiliation.Models;
using WalletWasabi.Fluent.Common.ViewModels.DialogBase;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.HomeScreen.WalletSettings.ViewModels;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ConfirmCoinjoinSettingsViewModel : DialogViewModelBase<Unit>
{
	private readonly WalletSettingsViewModel _settings;

	private ConfirmCoinjoinSettingsViewModel(WalletSettingsViewModel settings)
	{
		_settings = settings;
		Title = Resources.Coinjoin;
		SetupCancel(false, true, true);
		NextCommand = ReactiveCommand.Create(() =>
		{
			Close();
			settings.SelectedTab = 1;
			UiContext.Navigate(NavigationTarget.DialogScreen).To(settings);
		});
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_settings.WalletCoinJoinSettings.IsCoinjoinProfileSelected = true;
	}
}
