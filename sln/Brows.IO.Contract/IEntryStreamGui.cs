namespace Brows {
    public interface IEntryStreamGui {
        IEntryStreamSource Source { get; }
        IEntryStreamGuiState State { get; }
        IEntryStreamGuiOptions Options { get; }
    }
}
