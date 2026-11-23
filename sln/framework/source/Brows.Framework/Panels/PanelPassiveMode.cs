namespace Brows.Panels;

/// <summary>
/// Specifies how a command chooses its destination panel.
/// </summary>
public enum PanelPassiveMode {
    /// <summary>
    /// Does not choose a destination panel.
    /// </summary>
    None = 0,

    /// <summary>
    /// Uses the active panel when it is the only panel, or the other panel when there are two panels.
    /// With more panels, no destination is chosen.
    /// </summary>
    Auto,

    /// <summary>
    /// Uses the active panel as the destination.
    /// </summary>
    Active,

    /// <summary>
    /// Uses the column immediately after the active panel, if present.
    /// </summary>
    Next,

    /// <summary>
    /// Uses the column immediately before the active panel, if present.
    /// </summary>
    Previous
}
