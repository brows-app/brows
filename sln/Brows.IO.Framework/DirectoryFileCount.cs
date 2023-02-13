using System.IO;
using System.Threading;

namespace Brows {
    internal sealed class DirectoryFileCount : DirectoryItemCount {
        public DirectoryFileCount(string key, DirectoryInfo directory, CancellationToken cancellationToken) : base(key, directory, DirectoryItemCountKinds.File, cancellationToken) {
        }
    }
}
