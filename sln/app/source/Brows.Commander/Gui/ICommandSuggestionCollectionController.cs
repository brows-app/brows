using Brows.Commands;
using Brows.Triggers;
using System;

namespace Brows.Gui; 
public interface ICommandSuggestionCollectionController {
    event EventHandler CurrentSuggestionChanged;
    ICommandSuggestion CurrentSuggestion { get; }
    void MoveCurrentSuggestion(PressKey pressKey);
}
