namespace Brows {
    using Gui;

    partial class CommandPaletteInputControl {
        public CommandPaletteInputControl() {
            InitializeComponent();
            new CommandPaletteInputController(this);
        }
    }
}
