namespace Brows.Providers;

/// <summary>
/// Reports and requests focus for a provider view.
/// </summary>
public interface IProviderFocus {
    /// <summary>
    /// Requests keyboard focus for the view.
    /// </summary>
    /// <returns>
    /// True if the focus request succeeded; otherwise, false.
    /// </returns>
    bool Set();

    /// <summary>
    /// Determines whether the view has keyboard focus.
    /// </summary>
    /// <returns>
    /// True if the view has keyboard focus; otherwise, false.
    /// </returns>
    bool Get();
}
