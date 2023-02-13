using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Config;
    using FILECHECKSUM = IO.FileChecksum;

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
                        (nameof(FILECHECKSUM.ChecksumMD5),     250),
                        (nameof(FILECHECKSUM.ChecksumSHA1),    250),
                        (nameof(FILECHECKSUM.ChecksumSHA256),  250),
                        (nameof(FILECHECKSUM.ChecksumSHA512),  250),
                        (nameof(DirectorySize), 75),
                        (nameof(DirectoryFileCount), 50),
                        (nameof(DirectoryDirectoryCount), 50)
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
                    nameof(FileSystemInfoWrapper.LastWriteTime),
                    nameof(FileSystemInfoWrapper.Length)
                };
                Sorting = new Dictionary<string, EntrySortDirection?> {
                    { nameof(FileSystemInfoWrapper.Kind), EntrySortDirection.Ascending }
                };
            }
        }
    }
}
