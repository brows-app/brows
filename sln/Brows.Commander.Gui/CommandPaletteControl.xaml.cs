using Brows.Gui;

namespace Brows {
    partial class CommandPaletteControl {
        public CommandPaletteControl() {
            InitializeComponent();
            new CommandPaletteController(this);
        }
    }
}
