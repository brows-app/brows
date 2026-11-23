using System;

namespace Brows.Triggers;

/// <summary>
/// Specifies modifier keys held during a click gesture. Values can be combined.
/// </summary>
[Flags]
public enum ClickModifiers {
    /// <summary>
    /// No modifier keys.
    /// </summary>
    None = 0,

    /// <summary>
    /// The Alt modifier key.
    /// </summary>
    Alt = 1,

    /// <summary>
    /// The Control modifier key.
    /// </summary>
    Control = 2,

    /// <summary>
    /// The Shift modifier key.
    /// </summary>
    Shift = 4,

    /// <summary>
    /// The Windows modifier key.
    /// </summary>
    Windows = 8
}
