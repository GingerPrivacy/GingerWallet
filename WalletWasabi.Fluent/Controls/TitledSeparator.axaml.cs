using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Controls;

public class TitledSeparator : UserControl
{
	public static readonly StyledProperty<string> TitleProperty =
		AvaloniaProperty.Register<TitledSeparator, string>(nameof(Title));

	public string Title
	{
		get => GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public TitledSeparator()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
