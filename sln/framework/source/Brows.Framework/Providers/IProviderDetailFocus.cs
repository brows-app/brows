namespace Brows.Providers;

/// <summary>
/// Provides optional keyboard focus operations for a provider detail.
/// </summary>
public interface IProviderDetailFocus {
    /// <summary>
    /// Gets whether the detail currently owns keyboard focus.
    /// </summary>
    bool HasKeyboardFocus { get; }

    /// <summary>
    /// Requests keyboard focus for the detail.
    /// </summary>
    bool RequestFocus();
}