using Domore.Notification;

namespace Brows.Entries;

public abstract class EntryStreamGui : Notifier, IEntryStreamGui {
    public virtual bool ForceText => false;
    public virtual bool ForceImage => false;
    public virtual bool ForceMedia => false;
    public virtual bool ForcePreview => false;

    public string View {
        get;
        internal set => Change(ref field, value, nameof(View));
    }

    public bool Force =>
        ForceText || ForceImage || ForceMedia || ForcePreview;

    public abstract IEntryStreamSource Source { get; }
    public abstract IEntryStreamGuiOptions Options { get; }

    public IEntryStreamGuiState State => field ??= new EntryStreamGuiState(this) {
        ForceImage = ForceImage,
        ForceMedia = ForceMedia,
        ForcePreview = ForcePreview,
        ForceText = ForceText
    };
}
