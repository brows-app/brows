using System;

namespace Brows.Gui {
    public interface ICommandSuggestionCollectionController {
        event EventHandler CurrentSuggestionChanged;
        ICommandSuggestion CurrentSuggestion { get; }
        void MoveCurrentSuggestion(PressKey pressKey);
    }
}
