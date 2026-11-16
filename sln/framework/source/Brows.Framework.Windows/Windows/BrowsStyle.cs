using System.Windows;

namespace Brows.Windows;

internal static class BrowsStyle {
    public static readonly DependencyProperty BrowsStyleLevelProperty = DependencyProperty.RegisterAttached(
        name: "BrowsStyleLevel",
        propertyType: typeof(int),
        ownerType: typeof(BrowsStyle),
        defaultMetadata: new PropertyMetadata(
            defaultValue: 3));

    public static int GetBrowsStyleLevel(DependencyObject obj) {
        if (obj is null) {
            throw new ArgumentNullException(nameof(obj));
        }
        return (int)obj.GetValue(BrowsStyleLevelProperty);
    }

    public static void SetBrowsStyleLevel(DependencyObject obj, int value) {
        if (obj is null) {
            throw new ArgumentNullException(nameof(obj));
        }
        obj.SetValue(BrowsStyleLevelProperty, value);
    }

    public static readonly DependencyProperty BrowsStyleKindProperty = DependencyProperty.RegisterAttached(
        name: "BrowsStyleKind",
        propertyType: typeof(string),
        ownerType: typeof(BrowsStyle),
        defaultMetadata: new PropertyMetadata(
            defaultValue: null));

    public static string GetBrowsStyleKind(DependencyObject obj) {
        if (obj is null) {
            throw new ArgumentNullException(nameof(obj));
        }
        return (string)obj.GetValue(BrowsStyleKindProperty);
    }

    public static void SetBrowsStyleKind(DependencyObject obj, string value) {
        if (obj is null) {
            throw new ArgumentNullException(nameof(obj));
        }
        obj.SetValue(BrowsStyleKindProperty, value);
    }
}
