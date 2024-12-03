using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;

namespace WalletWasabi.Fluent.Controls
{
    public class SearchBarPopup : Popup
    {
        public static readonly StyledProperty<bool> IsCustomOpenProperty =
            AvaloniaProperty.Register<SearchBarPopup, bool>(nameof(IsCustomOpen));

        public bool IsCustomOpen
        {
            get => GetValue(IsCustomOpenProperty);
            set => SetValue(IsCustomOpenProperty, value);
        }

        private const double AllowedMaxHeight = 512;
        private const double AllowedMaxWidth = 456;

        private Animation? _expandAnimation = new()
        {
            Easing = Easing.Parse("0.4,0,0.6,1"),
            Duration = TimeSpan.FromMilliseconds(200),
            Children =
            {
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter(MaxHeightProperty, 0.0),
                        new Setter(MaxWidthProperty, 0.0),
                    },
                    Cue = new Cue(0)
                },
                new KeyFrame
                {
	                Setters =
	                {
		                new Setter(MaxHeightProperty, AllowedMaxHeight * 0.75),
		                new Setter(MaxWidthProperty, AllowedMaxWidth * 0.75),
	                },
	                Cue = new Cue(0.1)
                },
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter(MaxHeightProperty, AllowedMaxHeight),
                        new Setter(MaxWidthProperty, AllowedMaxWidth),
                    },
                    Cue = new Cue(1)
                },
            }
        };

        private Animation? _collapseAnimation = new()
        {
            Easing = Easing.Parse("0.4,0,0.6,1"),
            Duration = TimeSpan.FromMilliseconds(200),
            Children =
            {
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter(MaxHeightProperty, AllowedMaxHeight),
                        new Setter(MaxWidthProperty, AllowedMaxWidth),
                    },
                    Cue = new Cue(0)
                },
                new KeyFrame
                {
	                Setters =
	                {
		                new Setter(MaxHeightProperty, AllowedMaxHeight * 0.25),
		                new Setter(MaxWidthProperty, AllowedMaxWidth * 0.25),
	                },
	                Cue = new Cue(0.9)
                },
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter(MaxHeightProperty, 0.0),
                        new Setter(MaxWidthProperty, 0.0),
                    },
                    Cue = new Cue(1)
                },
            }
        };

        protected async override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsCustomOpenProperty && change.NewValue is bool isCustomOpen)
            {
                if (isCustomOpen)
                {
                    IsOpen = true;

                    if (_expandAnimation != null)
                    {
                        await _expandAnimation.RunAsync(this);
                        MaxHeight = AllowedMaxHeight;
                        MaxWidth = AllowedMaxWidth;
                    }
                }
                else
                {
                    if (_collapseAnimation != null)
                    {
                        await _collapseAnimation.RunAsync(this);
                        MaxHeight = 0;
                        MaxWidth = 0;
                    }

                    IsOpen = false;
                }
            }
        }
    }
}
