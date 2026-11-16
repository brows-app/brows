using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brows.Entries;

public sealed class EntryDataTextBlock : Control {
    public static readonly DependencyProperty TextProperty =
        TextBlock.TextProperty.AddOwner(typeof(EntryDataTextBlock));
    public static readonly DependencyProperty TextAlignmentProperty =
        TextBlock.TextAlignmentProperty.AddOwner(typeof(EntryDataTextBlock));
    public static readonly DependencyProperty TextDecorationsProperty =
        TextBlock.TextDecorationsProperty.AddOwner(typeof(EntryDataTextBlock));
    public static readonly DependencyProperty TextTrimmingProperty =
        TextBlock.TextTrimmingProperty.AddOwner(typeof(EntryDataTextBlock));
    public static readonly DependencyProperty TextWrappingProperty =
        TextBlock.TextWrappingProperty.AddOwner(typeof(EntryDataTextBlock));
    public static readonly DependencyProperty LineHeightProperty =
        TextBlock.LineHeightProperty.AddOwner(typeof(EntryDataTextBlock));
    public static readonly DependencyProperty LineStackingStrategyProperty =
        TextBlock.LineStackingStrategyProperty.AddOwner(typeof(EntryDataTextBlock));
    public static readonly DependencyProperty IsHyphenationEnabledProperty =
        TextBlock.IsHyphenationEnabledProperty.AddOwner(typeof(EntryDataTextBlock));
    public static readonly DependencyProperty TextEffectsProperty =
        TextBlock.TextEffectsProperty.AddOwner(typeof(EntryDataTextBlock));

    public string Text {
        get => GetValue(TextProperty) as string;
        set => SetValue(TextProperty, value);
    }

    public TextAlignment TextAlignment {
        get => (TextAlignment)GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    public TextDecorationCollection TextDecorations {
        get => GetValue(TextDecorationsProperty) as TextDecorationCollection;
        set => SetValue(TextDecorationsProperty, value);
    }

    public TextTrimming TextTrimming {
        get => (TextTrimming)GetValue(TextTrimmingProperty);
        set => SetValue(TextTrimmingProperty, value);
    }

    public TextWrapping TextWrapping {
        get => (TextWrapping)GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    public double LineHeight {
        get => (double)GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    public LineStackingStrategy LineStackingStrategy {
        get => (LineStackingStrategy)GetValue(LineStackingStrategyProperty);
        set => SetValue(LineStackingStrategyProperty, value);
    }

    public bool IsHyphenationEnabled {
        get => (bool)GetValue(IsHyphenationEnabledProperty);
        set => SetValue(IsHyphenationEnabledProperty, value);
    }

    public TextEffectCollection TextEffects {
        get => GetValue(TextEffectsProperty) as TextEffectCollection;
        set => SetValue(TextEffectsProperty, value);
    }
}
