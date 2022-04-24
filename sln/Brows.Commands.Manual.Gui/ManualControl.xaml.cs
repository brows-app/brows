namespace Brows {
    using Gui;

    partial class ManualControl {
        public ManualControl() {
            InitializeComponent();
            new ManualController(this);
        }
    }
}
