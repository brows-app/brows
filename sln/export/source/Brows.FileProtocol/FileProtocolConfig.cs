namespace Brows;

public abstract class FileProtocolConfig : EntryConfig {
    internal const int StreamSourceConcurrencyDefault = 2;

    /// <summary>
    /// Gets or sets the display options for previews of remote file entries.
    /// </summary>
    public EntryStreamGuiOptions Stream {
        get => field ??= new();
        set;
    }

    /// <summary>
    /// Gets or sets the maximum number of files downloaded concurrently when remote entries are
    /// copied to the local file system. The default value is 2.
    /// </summary>
    public int StreamSourceConcurrency {
        get;
        set;
    } = StreamSourceConcurrencyDefault;
}
