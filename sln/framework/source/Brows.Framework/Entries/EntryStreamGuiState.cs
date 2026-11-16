using Domore.Notification;

namespace Brows.Entries;

internal sealed class EntryStreamGuiState : Notifier, IEntryStreamGuiState {
    private static readonly object Loading = new();
    private static readonly object Unavailable = new();

    public EntryStreamGuiView Text => field ??= new(this);
    public EntryStreamGuiView Image => field ??= new(this);
    public EntryStreamGuiView Preview => field ??= new(this);
    public EntryStreamGuiView Media => field ??= new(this);

    public bool Force => ForceText || ForceImage || ForceMedia || ForcePreview;

    public bool ForceText {
        get;
        set {
            if (Change(ref field, value, nameof(ForceText), nameof(Force))) {
                Changed();
            }
        }
    }

    public bool ForceImage {
        get;
        set {
            if (Change(ref field, value, nameof(ForceImage), nameof(Force))) {
                Changed();
            }
        }
    }

    public bool ForceMedia {
        get;
        set {
            if (Change(ref field, value, nameof(ForceMedia), nameof(Force))) {
                Changed();
            }
        }
    }

    public bool ForcePreview {
        get;
        set {
            if (Change(ref field, value, nameof(ForcePreview), nameof(Force))) {
                Changed();
            }
        }
    }

    public EntryStreamGui Gui { get; }

    public EntryStreamGuiState(EntryStreamGui gui) {
        Gui = gui;
    }

    public void Changed() {
        if (Text.Success) {
            Gui.View = nameof(Text);
            return;
        }
        if (Media.Success) {
            Gui.View = nameof(Media);
            return;
        }
        if (Preview.Success) {
            Gui.View = nameof(Preview);
            return;
        }
        if (Image.Success) {
            Gui.View = nameof(Image);
            return;
        }
        if (ForceText && Text.Changed && Text.Loading == false) {
            Gui.View = nameof(Unavailable);
            return;
        }
        if (ForceImage && Image.Changed && Image.Loading == false) {
            Gui.View = nameof(Unavailable);
            return;
        }
        if (ForceMedia && Media.Changed && Media.Loading == false) {
            Gui.View = nameof(Unavailable);
            return;
        }
        if (ForcePreview && Preview.Changed && Preview.Loading == false) {
            Gui.View = nameof(Unavailable);
            return;
        }
        if (Text.Loading || Image.Loading || Media.Loading || Preview.Loading) {
            Gui.View = nameof(Loading);
            return;
        }
        if (Text.Changed || Image.Changed || Media.Changed || Preview.Changed) {
            Gui.View = nameof(Unavailable);
            return;
        }
        Gui.View = null;
    }

    IEntryStreamGuiView IEntryStreamGuiState.Text => Text;
    IEntryStreamGuiView IEntryStreamGuiState.Image => Image;
    IEntryStreamGuiView IEntryStreamGuiState.Media => Media;
    IEntryStreamGuiView IEntryStreamGuiState.Preview => Preview;
}
