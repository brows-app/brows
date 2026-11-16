using System.Windows;

namespace Brows;

internal sealed class CommanderWindow : Window {
    public static readonly DependencyProperty CommanderProperty = DependencyProperty.Register(
        name: nameof(Commander),
        propertyType: typeof(object),
        ownerType: typeof(CommanderWindow),
        typeMetadata: new PropertyMetadata(defaultValue: default(object)));

    public object Commander {
        get => GetValue(CommanderProperty);
        set => SetValue(CommanderProperty, value);
    }

    public CommanderWindow() {
    }
}
