using System;
using System.IO;

namespace Brows {
    public class FindItem {
        public string Name =>
            Info is DirectoryInfo
                ? Info.Name + Path.DirectorySeparatorChar
                : Info.Name;

        public string FullPath => Info.FullName;
        public string RelativeDirectory => Path.GetDirectoryName(FullPath).Replace(FindRoot.FullName, "").TrimStart(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        public FileSystemInfo Info { get; }
        public DirectoryInfo FindRoot { get; }

        public FindItem(FileSystemInfo info, DirectoryInfo findRoot) {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            FindRoot = findRoot ?? throw new ArgumentNullException(nameof(findRoot));
        }
    }
}
