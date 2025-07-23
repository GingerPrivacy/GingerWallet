using System.Collections;
using System.Linq;
using System.Reactive.Disposables;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.Controls;

public enum DefaultCommandSource
{
	None,
	Send,
	Receive
}

public class SubActionButton : ContentControl
{
	public static readonly StyledProperty<DefaultCommandSource> DefaultSourceProperty = AvaloniaProperty.Register<SubActionButton, DefaultCommandSource>(nameof(DefaultSource));

	public static readonly StyledProperty<StreamGeometry> IconProperty = AvaloniaProperty.Register<SubActionButton, StreamGeometry>(nameof(Icon));

	public static readonly StyledProperty<ICommand> CommandProperty = AvaloniaProperty.Register<SubActionButton, ICommand>(nameof(Command));

	public static readonly StyledProperty<ICommand> SetDefaultCommandProperty = AvaloniaProperty.Register<SubActionButton, ICommand>(nameof(SetDefaultCommand));

	public static readonly StyledProperty<UICommandCollection?> SubCommandsProperty = AvaloniaProperty.Register<SubActionButton, UICommandCollection?>(nameof(SubCommands));

	public static readonly StyledProperty<IEnumerable> ItemsProperty = AvaloniaProperty.Register<SubActionButton, IEnumerable>(nameof(Items));

	private CompositeDisposable? _disposable;

	public SubActionButton()
	{
		SetDefaultCommand = ReactiveCommand.Create<string>(key =>
		{
			var current = UiContext.Default.ApplicationSettings.DefaultCommands;

			if (DefaultSource == DefaultCommandSource.Receive)
			{
				UiContext.Default.ApplicationSettings.DefaultCommands = current with { ReceiveDefaultKey = key };
			}
			else if (DefaultSource == DefaultCommandSource.Send)
			{
				UiContext.Default.ApplicationSettings.DefaultCommands = current with { SendDefaultKey = key };
			}

			UpdateDefaultCommand(key);
		});
	}

	public DefaultCommandSource DefaultSource
	{
		get => GetValue(DefaultSourceProperty);
		set => SetValue(DefaultSourceProperty, value);
	}

	public StreamGeometry Icon
	{
		get => GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}

	private ICommand Command
	{
		get => GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	private ICommand SetDefaultCommand
	{
		get => GetValue(SetDefaultCommandProperty);
		set => SetValue(SetDefaultCommandProperty, value);
	}

	public UICommandCollection? SubCommands
	{
		get => GetValue(SubCommandsProperty);
		set => SetValue(SubCommandsProperty, value);
	}

	public IEnumerable Items
	{
		get => GetValue(ItemsProperty);
		set => SetValue(ItemsProperty, value);
	}

	private string? GetDefaultCommandKey()
	{
		var defaults = UiContext.Default.ApplicationSettings.DefaultCommands;

		return DefaultSource switch
		{
			DefaultCommandSource.Send => defaults.SendDefaultKey,
			DefaultCommandSource.Receive => defaults.ReceiveDefaultKey,
			_ => null
		};
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == SubCommandsProperty)
		{
			UpdateDefaultCommand(GetDefaultCommandKey());
		}
	}

	protected override void OnLoaded(RoutedEventArgs e)
	{
		base.OnLoaded(e);
		UpdateDefaultCommand(GetDefaultCommandKey());

		_disposable = new();
		UiContext.Default.ApplicationSettings
			.WhenAnyValue(x => x.DefaultCommands)
			.Subscribe(_ => UpdateDefaultCommand(GetDefaultCommandKey()))
			.DisposeWith(_disposable);
	}

	protected override void OnUnloaded(RoutedEventArgs e)
	{
		base.OnUnloaded(e);

		_disposable?.Dispose();
		_disposable = null;
	}

	private void UpdateDefaultCommand(string? key)
	{
		if (SubCommands is null)
		{
			return;
		}

		// Set initial default if there is none
		if (!SubCommands.Any(x => x.IsDefault))
		{
			var first = SubCommands.FirstOrDefault(x => x.Command is not null);
			if (first?.Command is not null)
			{
				first.IsDefault = true;
				SetValue(CommandProperty, first.Command);
			}
		}

		if (DefaultSource is DefaultCommandSource.None || key is null)
		{
			return;
		}

		var currentDefault = SubCommands.FirstOrDefault(x => x.IsDefault);

		if (SubCommands.FirstOrDefault(x => x.Key == key) is { } item &&
		    item != currentDefault &&
		    item.Command is not null)
		{
			if (currentDefault is not null)
			{
				currentDefault.IsDefault = false;
			}

			item.IsDefault = true;
			SetValue(CommandProperty, item.Command);
		}
	}
}
