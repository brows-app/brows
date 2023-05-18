using Brows.Gui;

namespace Brows {
    partial class PanelCollectionControl {
        public PanelCollectionControl() {
            InitializeComponent();
            new PanelCollectionController(this);
        }
    }
}
