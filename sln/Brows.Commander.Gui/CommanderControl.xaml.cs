using Brows.Gui;

namespace Brows {
    sealed partial class CommanderControl {
        public CommanderControl() {
            InitializeComponent();
            new CommanderController(this);
        }
    }
}
