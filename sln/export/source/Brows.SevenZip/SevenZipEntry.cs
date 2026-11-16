using Brows.Entries;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows;

internal sealed class SevenZipEntry : Entry<SevenZipProvider> {
    public new SevenZipProvider Provider =>
        base.Provider;

    public sealed override string Name => field ??= $"{Info.Kind}>{Info.Name.Normalized}";

    public sealed override string ID => field ??= $"{Provider.ID}>{Name}";

    public SevenZipStreamGui Stream =>
        new SevenZipStreamGui(this);

    public IEnumerable<SevenZipEntry> Children =>
        Info.Kind == SevenZipEntryKind.File
            ? Array.Empty<SevenZipEntry>()
            : Provider.Archive.ArchivePath.EntryInfo()?.Items?
                .Where(i => i.Name.Parent == Info.Name.Normalized)?
                .Select(i => new SevenZipEntry(Provider, i)) ?? Array.Empty<SevenZipEntry>();

    public IEnumerable<SevenZipEntry> Descendants =>
        Children.SelectMany(child => child.Descendants.Prepend(child));

    public SevenZipEntryInfo Info { get; }

    public SevenZipEntry(SevenZipProvider provider, SevenZipEntryInfo info) : base(provider) {
        Info = info ?? throw new ArgumentNullException(nameof(info));
    }
}
