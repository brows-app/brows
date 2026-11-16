using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brows.Entries;

public sealed class EntryThumbnailImage : Control {
    public static readonly DependencyProperty SourceProperty =
        Image.SourceProperty.AddOwner(typeof(EntryThumbnailImage));
    public static readonly DependencyProperty StretchProperty =
        Image.StretchProperty.AddOwner(typeof(EntryThumbnailImage));
    public static readonly DependencyProperty StretchDirectionProperty =
        Image.StretchDirectionProperty.AddOwner(typeof(EntryThumbnailImage));

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
