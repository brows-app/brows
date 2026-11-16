namespace Brows;

sealed partial class FileSystemFindResultControl {
    public FileSystemFindResultControl() {
        InitializeComponent();
        _ = new FileSystemFindResultController(this);
    }
}
