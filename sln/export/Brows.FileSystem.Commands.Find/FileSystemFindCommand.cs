using System.Collections.Generic;

namespace Brows {
    internal sealed class FileSystemFindCommand {
        public IList<FileSystemFindResult> List { get; } = new List<FileSystemFindResult>();
    }
}
