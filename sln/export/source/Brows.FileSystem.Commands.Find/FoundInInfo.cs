using System.IO;

namespace Brows;

internal class FoundInInfo {
    protected FoundInInfo() {
    }

    private string GetRelativeDirectory() {
        var directory = Path.GetDirectoryName(FullPath) ?? FindRoot.FullName;
        var relative = Path.GetRelativePath(FindRoot.FullName, directory);
        return relative == "."
            ? string.Empty
            : relative;
    }

    public string Name =>
        Info is DirectoryInfo
            ? Info.Name + Path.DirectorySeparatorChar
            : Info.Name;

    public string FullPath =>
        Info.FullName;

    public string TargetDirectory =>
        Info is DirectoryInfo directory
            ? directory.FullName
            : Path.GetDirectoryName(Info.FullName);

    public string RelativeDirectory =>
        GetRelativeDirectory();

    public FileSystemInfo Info { get; }
    public DirectoryInfo FindRoot { get; }

    public FoundInInfo(FileSystemInfo info, DirectoryInfo findRoot) {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        FindRoot = findRoot ?? throw new ArgumentNullException(nameof(findRoot));
    }
}
