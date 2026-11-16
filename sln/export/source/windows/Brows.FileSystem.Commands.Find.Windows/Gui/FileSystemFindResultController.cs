namespace Brows.Gui;

internal class FileSystemFindResultController : CollectionController<IFileSystemFindResultController, FoundInInfo>,
                                                IFileSystemFindResultController {
    internal FileSystemFindResultController(FileSystemFindResultControl element)
    : base(element, element.ItemsControl) {
    }
}
