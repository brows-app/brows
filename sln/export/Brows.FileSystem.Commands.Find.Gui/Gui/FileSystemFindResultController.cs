using System;

namespace Brows.Gui {
    internal class FileSystemFindResultController : CollectionController<IFileSystemFindResultController, FoundInInfo>, IFileSystemFindResultController {
        public new FileSystemFindResultControl Element { get; }

        public FileSystemFindResultController(FileSystemFindResultControl element) : base(element, element?.ListView) {
            Element = element ?? throw new ArgumentNullException(nameof(element));
        }
    }
}
