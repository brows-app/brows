using System.Windows.Input;

namespace Brows {
    using Gui;

    partial class PanelControl {
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e) {
            var panel = DataContext as Panel;
            if (panel != null) {
                panel.Activate();
            }
            base.OnPreviewMouseDown(e);
        }

        public PanelControl() {
            InitializeComponent();
            new PanelController(this);
        }
    }
}
