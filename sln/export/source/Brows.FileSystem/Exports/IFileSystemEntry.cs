namespace Brows.Exports;

public interface IFileSystemEntry : IEntry {
    string Path { get; }
    FileSystemEntryKind Kind { get; }
    IFileSystemProvider Provider { get; }
}
