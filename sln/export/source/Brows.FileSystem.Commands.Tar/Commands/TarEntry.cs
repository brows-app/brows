namespace Brows.Commands;

internal sealed record TarEntry {
    public string FilePath { get; init; }

    public string EntryName { get; init; }
}
