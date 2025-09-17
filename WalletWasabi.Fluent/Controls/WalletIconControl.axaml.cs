using Avalonia;
using Avalonia.Controls.Primitives;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Controls;

public class WalletIconControl : TemplatedControl
{
	public static readonly StyledProperty<WalletType> WalletTypeProperty = AvaloniaProperty.Register<WalletIconControl, WalletType>(nameof(WalletType));

	public static readonly StyledProperty<bool> IsNormalProperty = AvaloniaProperty.Register<WalletIconControl, bool>(nameof(IsNormal));

	public WalletType WalletType
	{
		get => GetValue(WalletTypeProperty);
		set => SetValue(WalletTypeProperty, value);
	}

	public bool IsNormal
	{
		get => GetValue(IsNormalProperty);
		set => SetValue(IsNormalProperty, value);
	}

	public WalletIconControl()
	{
		SetValue(IsNormalProperty, WalletType == WalletType.Normal);
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == WalletTypeProperty)
		{
			SetValue(IsNormalProperty, (WalletType)change.NewValue == WalletType.Normal);
		}
	}
}
