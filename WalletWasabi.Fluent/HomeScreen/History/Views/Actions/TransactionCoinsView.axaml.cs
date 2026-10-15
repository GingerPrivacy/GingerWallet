using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.TransactionViewer.ViewModels;
using WalletWasabi.Fluent.TransactionViewer.Views;

namespace WalletWasabi.Fluent.HomeScreen.History.Views.Actions;

public class TransactionCoinsView : UserControl
{
	private TransactionViewerWindow? _viewer;

	public TransactionCoinsView() => AvaloniaXamlLoader.Load(this);

	private void Open_OnClick(object? sender, RoutedEventArgs e)
	{
		if (_viewer is { })
		{
			_viewer.Activate();
			return;
		}
		if (DataContext is TransactionCoinsModel model && TopLevel.GetTopLevel(this) is Window owner)
		{
			var ui = UiContext.Default;
			_viewer = new TransactionViewerWindow
			{
				DataContext = new TransactionViewerViewModel(model, ui.AmountProvider.ExchangeRateObservable,
					ui.ApplicationSettings.WhenAnyValue(x => x.PrivacyMode))
			};
			_viewer.Closed += (_, _) => _viewer = null;
			_viewer.Show(owner);
		}
	}
}
