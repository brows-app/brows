using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Config;
    using IO;

    internal sealed class FileSystemColumn {
        private static readonly IComponentResourceKey Resolver = new FileSystemComponentKey();

        private PropSysConfig PropertySystem =>
            _PropertySystem ?? (
            _PropertySystem = PropSysConfig.Instance);
        private PropSysConfig _PropertySystem;

        public IReadOnlyList<FilePropertyData> Properties { get; private set; }
        public IReadOnlySet<string> Options { get; private set; }
        public IReadOnlySet<string> Default { get; private set; }
        public IReadOnlyDictionary<string, IEntryColumn> Columns { get; private set; }
        public IReadOnlyDictionary<string, EntrySortDirection?> Sorting { get; private set; }

        public async Task Init(CancellationToken cancellationToken) {
            await PropertySystem.Init(cancellationToken);
            var properties = PropertySystem.Properties;
            if (properties != Properties) {
                Properties = properties;
                Columns =
                    new (string, double)[] {
                    (Entry.ThumbnailKey, 100),
                    (nameof(FileInfoExtension.ChecksumMD5),     250),
                    (nameof(FileInfoExtension.ChecksumSHA1),    250),
                    (nameof(FileInfoExtension.ChecksumSHA256),  250),
                    (nameof(FileInfoExtension.ChecksumSHA512),  250),
                    (nameof(DirectorySize), 75)
                    }
                    .Concat(FileInfoData.Available.Select(d => (d.Key, d.Width)))
                    .Concat(Properties.Select(p => (p.Key, p.Width)))
                    .Select(t => (t.Item1, (IEntryColumn)new EntryColumn {
                        Resolver = Resolver,
                        Width = t.Item2
                    }))
                    .ToDictionary(t => t.Item1, t => t.Item2);
                Options = new HashSet<string>(Columns.Keys);
                Default = new HashSet<string> {
                    nameof(FileSystemInfoWrapper.Name),
                    nameof(FileSystemInfoWrapper.Length),
                    nameof(FileSystemInfoWrapper.LastWriteTime),
                    nameof(FileSystemInfoWrapper.Extension)
                };
                Sorting = new Dictionary<string, EntrySortDirection?> {
                    { nameof(FileSystemInfoWrapper.Kind), EntrySortDirection.Ascending }
                };
            }
        }
    }
}
