using System.ComponentModel;

namespace Brows.Entries;

/// <summary>
/// Tracks loading results for the available kinds of entry content view.
/// </summary>
public interface IEntryStreamGuiState : INotifyPropertyChanged {
    /// <summary>
    /// Gets the loading state to update for the text view.
    /// </summary>
    IEntryStreamGuiView Text { get; }

    /// <summary>
    /// Gets the loading state to update for the image view.
    /// </summary>
    IEntryStreamGuiView Image { get; }

    /// <summary>
    /// Gets the loading state to update for the media view.
    /// </summary>
    IEntryStreamGuiView Media { get; }

    /// <summary>
    /// Gets the loading state to update for the platform preview.
    /// </summary>
    IEntryStreamGuiView Preview { get; }
}
