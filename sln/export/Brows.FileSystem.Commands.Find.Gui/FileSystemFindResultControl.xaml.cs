using Brows.Gui;

namespace Brows {
    sealed partial class FileSystemFindResultControl {
        public FileSystemFindResultControl() {
            InitializeComponent();
            new FileSystemFindResultController(this);
        }
    }
}
