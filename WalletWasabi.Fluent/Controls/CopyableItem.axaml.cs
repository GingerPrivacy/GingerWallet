using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls;

public class CopyableItem : ContentControl
{
	public static readonly StyledProperty<string?> ContentToCopyProperty = AvaloniaProperty.Register<CopyableItem, string?>(nameof(ContentToCopy));
	public static readonly StyledProperty<bool> CopyButtonEnabledProperty = AvaloniaProperty.Register<CopyableItem, bool>(nameof(CopyButtonEnabled), true);

	public string? ContentToCopy
	{
		get => GetValue(ContentToCopyProperty);
		set => SetValue(ContentToCopyProperty, value);
	}

	public bool CopyButtonEnabled
	{
		get => GetValue(CopyButtonEnabledProperty);
		set => SetValue(CopyButtonEnabledProperty, value);
	}
}
