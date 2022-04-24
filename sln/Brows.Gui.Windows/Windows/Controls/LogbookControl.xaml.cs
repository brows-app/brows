namespace Brows.Windows.Controls {
    using Gui;

    partial class LogbookControl {
        public LogbookControl() {
            InitializeComponent();
            new LogbookController(this);
        }
    }
}
