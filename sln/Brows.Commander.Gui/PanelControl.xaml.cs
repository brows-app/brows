namespace Brows {
    using Gui;

    partial class PanelControl {
        public PanelControl() {
            InitializeComponent();
            new PanelController(this);
        }
    }
}
