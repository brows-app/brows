namespace Brows.MBox.Format;

/// <summary>
/// Describes how completely the body of a message was read.
/// </summary>
public enum MBoxBodyState {
    /// <summary>
    /// The body was not read.
    /// </summary>
    NotRead,

    /// <summary>
    /// The body was read completely.
    /// </summary>
    Complete,

    /// <summary>
    /// The body was read, but a limit cut the result short.
    /// </summary>
    Truncated,

    /// <summary>
    /// The body cannot be displayed.
    /// </summary>
    Unavailable,

    /// <summary>
    /// The body is malformed.
    /// </summary>
    Malformed,
}
