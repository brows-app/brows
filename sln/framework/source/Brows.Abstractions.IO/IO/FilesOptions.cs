namespace Brows.IO;

public sealed class FilesOptions {
    public string DefaultExtension { get; init; }
    public string Filter { get; init; }
    public string InitialDirectory { get; init; }
    public bool Multiselect { get; init; }
    public string Title { get; init; }
}
