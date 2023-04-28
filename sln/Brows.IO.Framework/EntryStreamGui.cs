using Domore.Notification;

namespace Brows {
    public abstract class EntryStreamGui : Notifier, IEntryStreamGui {
        public virtual bool ForceText => false;
        public virtual bool ForceImage => false;
        public virtual bool ForcePreview => false;

        public string View {
            get => _View;
            internal set => Change(ref _View, value, nameof(View));
        }
        private string _View;

        public bool Force =>
            ForceText || ForceImage || ForcePreview;

        public abstract IEntryStreamSource Source { get; }
        public abstract IEntryStreamGuiOptions Options { get; }

        public IEntryStreamGuiState State =>
            _State ?? (
            _State = new EntryStreamGuiState(this) {
                ForceImage = ForceImage,
                ForcePreview = ForcePreview,
                ForceText = ForceText
            });
        private IEntryStreamGuiState _State;
    }
}
