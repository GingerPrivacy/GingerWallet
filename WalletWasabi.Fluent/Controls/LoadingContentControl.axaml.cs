using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls;

public class LoadingContentControl : ContentControl
{
	public static readonly StyledProperty<bool> IsLoadingProperty =
		AvaloniaProperty.Register<LoadingContentControl, bool>(nameof(IsLoading));

	public static readonly StyledProperty<bool> LoadingAnimationEnabledProperty =
		AvaloniaProperty.Register<LoadingContentControl, bool>(nameof(LoadingAnimationEnabled), true);

	public bool IsLoading
	{
		get => GetValue(IsLoadingProperty);
		set => SetValue(IsLoadingProperty, value);
	}

	public bool LoadingAnimationEnabled
	{
		get => GetValue(LoadingAnimationEnabledProperty);
		set => SetValue(LoadingAnimationEnabledProperty, value);
	}
}
