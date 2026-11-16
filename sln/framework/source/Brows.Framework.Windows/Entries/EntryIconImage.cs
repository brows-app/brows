using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brows.Entries;

public sealed class EntryIconImage : Control {
    public static readonly DependencyProperty SourceProperty = Image.SourceProperty.AddOwner(typeof(EntryIconImage));
    public static readonly DependencyProperty StretchProperty = Image.StretchProperty.AddOwner(typeof(EntryIconImage));
    public static readonly DependencyProperty StretchDirectionProperty =
        Image.StretchDirectionProperty.AddOwner(typeof(EntryIconImage));

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
