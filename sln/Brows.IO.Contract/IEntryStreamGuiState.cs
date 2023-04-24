namespace Brows {
    public interface IEntryStreamGuiState {
        IEntryStreamGuiView Text { get; }
        IEntryStreamGuiView Image { get; }
        IEntryStreamGuiView Preview { get; }
    }
}
