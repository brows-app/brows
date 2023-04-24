using Domore.Notification;

namespace Brows {
    internal sealed class EntryStreamGuiState : Notifier, IEntryStreamGuiState {
        public IEntryStreamGuiView Text =>
            _Text ?? (
            _Text = new EntryStreamGuiView());
        private IEntryStreamGuiView _Text;

        public IEntryStreamGuiView Image =>
            _Image ?? (
            _Image = new EntryStreamGuiView());
        private IEntryStreamGuiView _Image;

        public IEntryStreamGuiView Preview =>
            _Preview ?? (
            _Preview = new EntryStreamGuiView());
        private IEntryStreamGuiView _Preview;
    }
}
