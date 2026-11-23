namespace Brows.Security;

/// <summary>
/// Specifies how sensitive text is stored.
/// </summary>
public enum SecurityDataKind {
    /// <summary>
    /// Stores text without protecting it.
    /// </summary>
    PlainText,

    /// <summary>
    /// Stores text in protected form.
    /// </summary>
    Protected
}
