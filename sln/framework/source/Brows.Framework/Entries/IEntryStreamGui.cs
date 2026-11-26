using System.ComponentModel;

namespace Brows.Entries;

/// <summary>
/// Provides the source, options, and loading state for an entry content view.
/// </summary>
public interface IEntryStreamGui : INotifyPropertyChanged {
    /// <summary>
    /// Gets the active view name, such as Text, Markdown, Image, Media, Preview, Loading, or Unavailable.
    /// The value is null before a view is chosen.
    /// </summary>
    string View { get; }

    /// <summary>
    /// Gets the entry content source to display.
    /// </summary>
    IEntryStreamSource Source { get; }

    /// <summary>
    /// Gets the loading state reported by the content views.
    /// </summary>
    IEntryStreamGuiState State { get; }

    /// <summary>
    /// Gets the settings used to load and display the content.
    /// </summary>
    IEntryStreamGuiOptions Options { get; }
}
