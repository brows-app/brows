using System;

namespace Brows.Gui {
    public interface ICommandPaletteController : IController {
        event EventHandler CurrentSuggestionChanged;
        event EventHandler LostFocus;
        event CommanderPressEventHandler KeyboardKeyDown;

        ICommandSuggestion CurrentSuggestion { get; }

        void MoveCaret(int index);
        void SelectText(int start, int length);
        void ScrollSuggestionData(PressKey key);
    }
}
