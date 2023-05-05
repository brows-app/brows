using Domore.Notification;

namespace Brows {
    internal sealed class EntryStreamGuiState : Notifier, IEntryStreamGuiState {
        private static readonly object Loading = new();
        private static readonly object Unavailable = new();

        public EntryStreamGuiView Text =>
            _Text ?? (
            _Text = new(this));
        private EntryStreamGuiView _Text;

        public EntryStreamGuiView Image =>
            _Image ?? (
            _Image = new(this));
        private EntryStreamGuiView _Image;

        public EntryStreamGuiView Preview =>
            _Preview ?? (
            _Preview = new(this));
        private EntryStreamGuiView _Preview;

        public EntryStreamGuiView Media =>
            _Media ?? (
            _Media = new(this));
        private EntryStreamGuiView _Media;

        public bool Force =>
            ForceText || ForceImage || ForceMedia || ForcePreview;

        public bool ForceText {
            get => _ForceText;
            set {
                if (Change(ref _ForceText, value, nameof(ForceText), nameof(Force))) {
                    Changed();
                }
            }
        }
        private bool _ForceText;

        public bool ForceImage {
            get => _ForceImage;
            set {
                if (Change(ref _ForceImage, value, nameof(ForceImage), nameof(Force))) {
                    Changed();
                }
            }
        }
        private bool _ForceImage;

        public bool ForceMedia {
            get => _ForceMedia;
            set {
                if (Change(ref _ForceMedia, value, nameof(ForceMedia), nameof(Force))) {
                    Changed();
                }
            }
        }
        private bool _ForceMedia;

        public bool ForcePreview {
            get => _ForcePreview;
            set {
                if (Change(ref _ForcePreview, value, nameof(ForcePreview), nameof(Force))) {
                    Changed();
                }
            }
        }
        private bool _ForcePreview;

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
}
