namespace Brows {
    public interface IFileSystemEntry : IEntry {
        FileSystemEntryKind Kind { get; }
    }
}
