using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal sealed class ZipEntry : Entry<ZipProvider> {
        public new ZipProvider Provider =>
            base.Provider;

        public sealed override string Name =>
            _Name ?? (
            _Name = $"{Info.Kind}>{Info.Name.Normalized}");
        private string _Name;

        public sealed override string ID =>
            _ID ?? (
            _ID = $"{Provider.ID}>{Name}");
        private string _ID;

        public ZipStreamGui Stream =>
            new ZipStreamGui(this);

        public IEnumerable<ZipEntry> Children =>
            Info.Kind == ZipEntryKind.File
                ? Array.Empty<ZipEntry>()
                : Provider.Zip.ArchivePath.EntryInfo()?.Items?
                    .Where(i => i.Name.Parent == Info.Name.Normalized)?
                    .Select(i => new ZipEntry(Provider, i)) ?? Array.Empty<ZipEntry>();

        public IEnumerable<ZipEntry> Descendants =>
            Children.SelectMany(child => child.Descendants.Prepend(child));

        public ZipEntryInfo Info { get; }

        public ZipEntry(ZipProvider provider, ZipEntryInfo info) : base(provider) {
            Info = info ?? throw new ArgumentNullException(nameof(info));
        }
    }
}
