namespace Brows.Gui {
    public interface IPreviewArea {
        int Left { get; }
        int Right { get; }
        int Bottom { get; }
        int Top { get; }
        int BackgroundColor { get; }
        int ForegroundColor { get; }
    }
}
