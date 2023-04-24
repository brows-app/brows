namespace Brows {
    using Gui;

    partial class FileSystemFindResultControl {
        public FileSystemFindResultControl() {
            InitializeComponent();
            new FileSystemFindResultController(this);
        }
    }
}
