using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brows.Providers;

public sealed class ProviderTreeIconImage : Control {
    public static readonly DependencyProperty SourceProperty =
        Image.SourceProperty.AddOwner(typeof(ProviderTreeIconImage));
    public static readonly DependencyProperty StretchProperty =
        Image.StretchProperty.AddOwner(typeof(ProviderTreeIconImage));
    public static readonly DependencyProperty StretchDirectionProperty =
        Image.StretchDirectionProperty.AddOwner(typeof(ProviderTreeIconImage));

    public ImageSource Source {
        get => GetValue(SourceProperty) as ImageSource;
        set => SetValue(SourceProperty, value);
    }

    public Stretch Stretch {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public StretchDirection StretchDirection {
        get => (StretchDirection)GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }
}
