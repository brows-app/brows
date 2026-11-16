using Domore.Notification;

namespace Brows;

public sealed class CommanderTheme : Notifier {
    /// <summary>
    /// Gets or sets the name of the color theme, independently of its light or dark mode.
    /// </summary>
    public string Name {
        get;
        set => Change(ref field, value);
    } = "FileExplorer";

    /// <summary>
    /// Gets or sets the color mode. Font resources are independent of this setting.
    /// </summary>
    public CommanderThemeMode Mode {
        get;
        set => Change(ref field, value);
    } = CommanderThemeMode.Dark;
}
