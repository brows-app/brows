namespace Brows {
    using Gui;

    partial class CommanderControl {
        public CommanderControl() {
            InitializeComponent();
            new CommanderController(this);
        }
    }
}
