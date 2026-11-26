using Domore.Notification;

namespace Brows.Entries;

/// <summary>
/// Provides an entry content view with optional choices for how the content is displayed.
/// </summary>
public abstract class EntryStreamGui : Notifier, IEntryStreamGui {
    /// <summary>
    /// Gets whether the text view is explicitly requested. The default is false.
    /// </summary>
    public virtual bool ForceText => false;

    /// <summary>
    /// Gets whether the Markdown view is explicitly requested. The default is false.
    /// </summary>
    public virtual bool ForceMarkdown => false;

    /// <summary>
    /// Gets whether the image view is explicitly requested. The default is false.
    /// </summary>
    public virtual bool ForceImage => false;

    /// <summary>
    /// Gets whether the media view is explicitly requested. The default is false.
    /// </summary>
    public virtual bool ForceMedia => false;

    /// <summary>
    /// Gets whether a platform preview is explicitly requested. The default is false.
    /// </summary>
    public virtual bool ForcePreview => false;

    /// <summary>
    /// Gets the active view name, such as Text, Markdown, Image, Media, Preview, Loading, or Unavailable.
    /// The value is null before a view is chosen.
    /// </summary>
    public string View {
        get;
        internal set => Change(ref field, value, nameof(View));
    }

    /// <summary>
    /// Gets whether any particular content view has been explicitly requested.
    /// </summary>
    public bool Force =>
        ForceText || ForceMarkdown || ForceImage || ForceMedia || ForcePreview;

    /// <summary>
    /// Gets the entry content source to display.
    /// </summary>
    public abstract IEntryStreamSource Source { get; }

    /// <summary>
    /// Gets the settings used to load and display the content.
    /// </summary>
    public abstract IEntryStreamGuiOptions Options { get; }

    /// <summary>
    /// Gets the loading state reported by the content views.
    /// </summary>
    public IEntryStreamGuiState State => field ??= new EntryStreamGuiState(this) {
        ForceImage = ForceImage,
        ForceMarkdown = ForceMarkdown,
        ForceMedia = ForceMedia,
        ForcePreview = ForcePreview,
        ForceText = ForceText,
    };
}
