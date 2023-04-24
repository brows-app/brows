using System.IO;

namespace Brows {
    internal sealed class FoundInDirectoryName : FoundInInfo {
        public FoundInDirectoryName(DirectoryInfo info, DirectoryInfo findRoot) : base(info, findRoot) {
        }
    }
}
