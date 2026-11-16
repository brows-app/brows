namespace Brows.Commands;

internal sealed record SevenZipEntry {
    public string FilePath { get; init; }

    public string EntryName { get; init; }
}
