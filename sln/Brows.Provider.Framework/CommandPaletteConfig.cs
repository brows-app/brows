using System.Collections.Generic;

namespace Brows {
    public sealed class CommandPaletteConfig : ICommandPaletteConfig {
        public string Input { get; set; }
        public int SelectedStart { get; set; }
        public int SelectedLength { get; set; }
        public IEnumerable<ICommand> SuggestCommands { get; set; }
    }
}
