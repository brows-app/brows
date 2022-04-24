namespace Brows {
    using Gui;

    partial class FindResultControl {
        public FindResultControl() {
            InitializeComponent();
            new FindResultController(this);
        }
    }
}
