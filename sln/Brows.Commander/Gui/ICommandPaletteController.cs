using System;

namespace Brows.Gui {
    public interface ICommandPaletteController : ITriggerController {
        public event EventHandler CurrentSuggestionChanged;
        public event EventHandler LostFocus;

        ICommandSuggestion CurrentSuggestion { get; }

        void MoveCaret(int index);
        void SelectText(int start, int length);
    }
}
