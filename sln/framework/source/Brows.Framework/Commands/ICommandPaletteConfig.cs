using System.Collections.Generic;

namespace Brows.Commands;

public interface ICommandPaletteConfig {
    string Input { get; }
    int SelectedStart { get; }
    int SelectedLength { get; }
    public IEnumerable<ICommand> SuggestCommands { get; }
}
