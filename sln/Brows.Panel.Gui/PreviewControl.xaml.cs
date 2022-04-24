namespace Brows {
    using Gui;

    partial class PreviewControl {
        public PreviewControl() {
            InitializeComponent();
            new PreviewController(this);
        }
    }
}
