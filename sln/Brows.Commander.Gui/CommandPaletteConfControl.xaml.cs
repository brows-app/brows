namespace Brows {
    using Gui;

    partial class CommandPaletteConfControl {
        public CommandPaletteConfControl() {
            InitializeComponent();
            new CommandPaletteConfController(this);
        }
    }
}
