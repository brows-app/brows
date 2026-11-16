using System;
using System.IO;

namespace Brows;

public abstract class FileProtocolEntry : Entry {
    public abstract Uri Uri { get; }
    public abstract FileProtocolEntryKind Kind { get; }

    public string Extension => field ??= Path.GetExtension(Name) ?? "";

    public sealed override string ID => field ??= Uri.ToString();

    protected FileProtocolEntry(EntryProvider provider) : base(provider) {
    }
}

public abstract class FileProtocolEntry<TProvider> : FileProtocolEntry where TProvider : EntryProvider {
    protected new TProvider Provider { get; }

    protected FileProtocolEntry(TProvider provider) : base(provider) {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }
}
