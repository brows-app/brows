using System.IO;

namespace Brows {
    internal sealed class FoundInFileName : FoundInInfo {
        public FoundInFileName(FileInfo info, DirectoryInfo findRoot) : base(info, findRoot) {
        }
    }
}
