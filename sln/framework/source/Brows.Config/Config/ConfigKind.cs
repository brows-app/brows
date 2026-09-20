namespace Brows.Config;

/// <summary>
/// The available config kinds.
/// </summary>
public enum ConfigKind {
    /// <summary>
    /// No configuration.
    /// </summary>
    None = 0,

    /// <summary>
    /// Configuration is user-specific.
    /// </summary>
    User,

    /// <summary>
    /// Configuration is user-agnostic.
    /// </summary>
    Common
}
