using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.TransactionViewer.ViewModels;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.TransactionViewer.Views;

public class TransactionViewerWindow : Window
{
	public TransactionViewerWindow() => AvaloniaXamlLoader.Load(this);

	protected override void OnClosed(EventArgs e)
	{
		var viewModel = DataContext as TransactionViewerViewModel;
		DataContext = null;
		viewModel?.Dispose();
		base.OnClosed(e);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
			e.Handled = true;
		}
	}

	private async void Copy_OnClick(object? sender, RoutedEventArgs e)
	{
		if (DataContext is TransactionViewerViewModel { PrivacyMode: false } && sender is Button { Tag: { } value } && Clipboard is { } clipboard)
		{
			try
			{
				await clipboard.SetTextAsync(value.ToString());
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}
	}

	private void Close_OnClick(object? sender, RoutedEventArgs e) => Close();
}
