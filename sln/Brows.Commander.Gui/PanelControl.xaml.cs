using Brows.Gui;

namespace Brows {
    partial class PanelControl {
        public PanelControl() {
            InitializeComponent();
            new PanelController(this);
        }
    }
}
