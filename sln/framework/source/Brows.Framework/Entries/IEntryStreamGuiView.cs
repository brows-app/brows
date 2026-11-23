namespace Brows.Entries;

/// <summary>
/// Reports loading and success changes for one kind of entry content view.
/// </summary>
public interface IEntryStreamGuiView {
    /// <summary>
    /// Reports changes to loading and display success for this view.
    /// </summary>
    /// <param name="loading">
    /// Whether the view is loading, or null to keep the current state.
    /// </param>
    /// <param name="success">
    /// Whether the view can display the content, or null to keep the current state.
    /// </param>
    void Change(bool? loading = null, bool? success = null);
}
