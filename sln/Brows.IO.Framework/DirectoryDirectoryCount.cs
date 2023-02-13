using System.IO;
using System.Threading;

namespace Brows {
    internal sealed class DirectoryDirectoryCount : DirectoryItemCount {
        public DirectoryDirectoryCount(string key, DirectoryInfo directory, CancellationToken cancellationToken) : base(key, directory, DirectoryItemCountKinds.Directory, cancellationToken) {
        }
    }
}
