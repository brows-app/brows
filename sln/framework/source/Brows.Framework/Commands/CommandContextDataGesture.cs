using Brows.Triggers;

namespace Brows.Commands;

/// <summary>
/// Provides the keyboard gestures used to browse and edit command data.
/// </summary>
public class CommandContextDataGesture {
    /// <summary>
    /// Initializes a command data gesture definition.
    /// </summary>
    protected CommandContextDataGesture() {
    }

    /// <summary>
    /// Gets the gesture for moving upward through displayed command data.
    /// </summary>
    public static PressGesture Up { get; } = new PressGesture(PressKey.Up, PressModifiers.Alt);

    /// <summary>
    /// Gets the gesture for moving downward through displayed command data.
    /// </summary>
    public static PressGesture Down { get; } = new PressGesture(PressKey.Down, PressModifiers.Alt);

    /// <summary>
    /// Gets the gesture for moving upward by a page of command data.
    /// </summary>
    public static PressGesture PageUp { get; } = new PressGesture(PressKey.PageUp, PressModifiers.Alt);

    /// <summary>
    /// Gets the gesture for moving downward by a page of command data.
    /// </summary>
    public static PressGesture PageDown { get; } = new PressGesture(PressKey.PageDown, PressModifiers.Alt);

    /// <summary>
    /// Gets the gesture for choosing the previous data item.
    /// </summary>
    public static PressGesture Previous { get; } = new PressGesture(PressKey.Left, PressModifiers.Alt);

    /// <summary>
    /// Gets the gesture for choosing the next data item.
    /// </summary>
    public static PressGesture Next { get; } = new PressGesture(PressKey.Right, PressModifiers.Alt);

    /// <summary>
    /// Gets the gesture for accepting the current data item.
    /// </summary>
    public static PressGesture Enter { get; } = new PressGesture(PressKey.Enter, PressModifiers.Alt);

    /// <summary>
    /// Gets the gesture for removing the current data item.
    /// </summary>
    public static PressGesture Remove { get; } = new PressGesture(PressKey.D3, PressModifiers.Shift | PressModifiers.Alt);

    /// <summary>
    /// Gets the gesture for clearing all command data.
    /// </summary>
    public static PressGesture Clear { get; } = new PressGesture(PressKey.D3, PressModifiers.Shift | PressModifiers.Alt | PressModifiers.Control);
}
