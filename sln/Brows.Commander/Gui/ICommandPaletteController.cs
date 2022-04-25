using System;

namespace Brows.Gui {
    using Triggers;

    public interface ICommandPaletteController : IController {
        event EventHandler CurrentSuggestionChanged;
        event EventHandler LostFocus;
        event InputEventHandler Input;
        event KeyboardKeyEventHandler KeyboardKeyDown;

        ICommandSuggestion CurrentSuggestion { get; }

        void MoveCaret(int index);
        void SelectText(int start, int length);
        void ScrollSuggestionData(KeyboardKey key);
    }
}
