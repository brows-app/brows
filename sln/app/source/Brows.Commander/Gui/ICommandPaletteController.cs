using Brows.Triggers;

namespace Brows.Gui; 
public interface ICommandPaletteController : IController {
    event GestureEventHandler Gesture;
    void ScrollSuggestionData(PressKey key);
}
