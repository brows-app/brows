namespace Brows.Gui {
    public interface ICommandPaletteConfController {
        event GestureEventHandler Gesture;
        void Focus();
        int CaretLine();
    }
}
