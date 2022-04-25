namespace Brows {
    using Gui;

    partial class CommandPaletteControl {
        public CommandPaletteControl() {
            InitializeComponent();
            new CommandPaletteController(this);
        }
    }
}
