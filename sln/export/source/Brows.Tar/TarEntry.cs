using Brows.Entries;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows;

internal sealed class TarEntry : Entry<TarProvider> {
    public new TarProvider Provider =>
        base.Provider;

    public sealed override string Name => field ??= $"{Info.Kind}>{Info.Name.Normalized}";

    public sealed override string ID => field ??= $"{Provider.ID}>{Name}";

    public TarStreamGui Stream =>
        new TarStreamGui(this);

    public IEnumerable<TarEntry> Children =>
        Info.Kind == TarEntryKind.File
            ? Array.Empty<TarEntry>()
            : Provider.Archive.ArchivePath.EntryInfo()?.Items?
                .Where(i => i.Name.Parent == Info.Name.Normalized)?
                .Select(i => new TarEntry(Provider, i)) ?? Array.Empty<TarEntry>();

    public IEnumerable<TarEntry> Descendants =>
        Children.SelectMany(child => child.Descendants.Prepend(child));

    public TarEntryInfo Info { get; }

    public TarEntry(TarProvider provider, TarEntryInfo info) : base(provider) {
        Info = info ?? throw new ArgumentNullException(nameof(info));
    }
}
