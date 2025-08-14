using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.TwoFactor.ViewModels;

namespace WalletWasabi.Fluent.TwoFactor.Controls;

public class TwoFactorEntryBox : TemplatedControl
{
	private CompositeDisposable _compositeDisposable;

	public static readonly StyledProperty<uint> DigitCountProperty =
		AvaloniaProperty.Register<AmountControl, uint>(nameof(DigitCount), 8);

	public static readonly StyledProperty<string> TextProperty =
		AvaloniaProperty.Register<AmountControl, string>(nameof(Text));

	public static readonly StyledProperty<ObservableCollectionExtended<TwoFactorNumberViewModel>> ItemsProperty =
		AvaloniaProperty.Register<AmountControl, ObservableCollectionExtended<TwoFactorNumberViewModel>>(nameof(Items));

	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
	public TwoFactorEntryBox()
	{
		Items = new ObservableCollectionExtended<TwoFactorNumberViewModel>();

		_compositeDisposable = new CompositeDisposable();

		var itemsSourceList = new SourceList<TwoFactorNumberViewModel>().DisposeWith(_compositeDisposable);

		itemsSourceList
			.Connect()
			.Bind(Items)
			.Subscribe()
			.DisposeWith(_compositeDisposable);

		itemsSourceList
			.Connect()
			.WhenPropertyChanged(x => x.Number)
			.Select(_ =>
			{
				var sb = new StringBuilder();
				foreach (var twoFactorNumberViewModel in Items)
				{
					sb.Append(twoFactorNumberViewModel.Number);
				}

				return sb.ToString();
			})
			.BindTo(this, x => x.Text)
			.DisposeWith(_compositeDisposable);


		for (var i = 0; i < DigitCount; i++)
		{
			itemsSourceList.Add(new TwoFactorNumberViewModel());
		}
	}

	public uint DigitCount
	{
		get => GetValue(DigitCountProperty);
		set => SetValue(DigitCountProperty, value);
	}

	public string Text
	{
		get => GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	private ObservableCollectionExtended<TwoFactorNumberViewModel> Items
	{
		get => GetValue(ItemsProperty);
		set => SetValue(ItemsProperty, value);
	}

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);

		var itemsControl = e.NameScope.Find<ItemsControl>("PART_NumberControls");

		if (itemsControl is not null)
		{
			itemsControl.Loaded += ItemsControlLoaded;
		}
	}

	private void ItemsControlLoaded(object? sender, RoutedEventArgs e)
	{
		var control = sender as ItemsControl;

		if (control is null)
		{
			return;
		}

		var textBoxes = control.GetLogicalChildren().Select(x => (x as ContentPresenter)?.Child as TextBox).ToArray();

		if (textBoxes.FirstOrDefault() is { } firstTb)
		{
			firstTb.Focus();
		}

		if (textBoxes[DigitCount / 2 - 1] is { } middleTb)
		{
			middleTb.Margin = new Thickness(0, 0, 10, 0);
		}
	}

	protected override void OnUnloaded(RoutedEventArgs e)
	{
		base.OnUnloaded(e);

		_compositeDisposable.Dispose();
	}
}
