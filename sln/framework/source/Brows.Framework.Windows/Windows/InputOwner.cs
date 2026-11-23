using System.Windows;

namespace Brows.Windows;

/// <summary>
/// Associates a WPF input subtree with an explicit input owner.
/// </summary>
public static class InputOwner {
    /// <summary>
    /// Identifies the inherited input owner attached property.
    /// </summary>
    public static readonly DependencyProperty OwnerProperty = DependencyProperty.RegisterAttached(
        name: "Owner",
        propertyType: typeof(object),
        ownerType: typeof(InputOwner),
        defaultMetadata: new FrameworkPropertyMetadata(
            defaultValue: null,
            flags: FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>
    /// Gets the input owner associated with an element, including an owner inherited from its ancestors.
    /// </summary>
    /// <param name="element">
    /// The element whose input owner is requested.
    /// </param>
    /// <returns>
    /// The associated owner, or <see langword="null"/> when no owner is associated.
    /// </returns>
    public static object GetOwner(DependencyObject element) {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(OwnerProperty);
    }

    /// <summary>
    /// Sets the input owner for an element and its descendants.
    /// </summary>
    /// <param name="element">
    /// The element that owns the input subtree.
    /// </param>
    /// <param name="owner">
    /// The input owner to associate with the element. A null value blocks an inherited owner.
    /// </param>
    public static void SetOwner(DependencyObject element, object owner) {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(OwnerProperty, owner);
    }
}
