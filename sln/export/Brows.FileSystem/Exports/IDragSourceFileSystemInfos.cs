using System.Collections.Generic;
using System.IO;

namespace Brows.Exports {
    public interface IDragSourceFileSystemInfos : IExport {
        void Drag(object source, IEnumerable<FileSystemInfo> fileSystemInfos);
    }
}
