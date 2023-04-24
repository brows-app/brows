namespace Brows {
    public abstract class EntryStreamGui : IEntryStreamGui {
        public abstract IEntryStreamSource Source { get; }
        public abstract IEntryStreamGuiOptions Options { get; }

        public IEntryStreamGuiState State => _State ??= new EntryStreamGuiState();
        private IEntryStreamGuiState _State;
    }
}
