using Brows.Gui;

namespace Brows {
    partial class CommandPaletteConfControl {
        public CommandPaletteConfControl() {
            InitializeComponent();
            new CommandPaletteConfController(this);
        }
    }
}
