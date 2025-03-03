using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.HomeScreen.History.Views.Actions;

public class TransactionDetailsView : UserControl
{
	public TransactionDetailsView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
