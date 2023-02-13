using Domore.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal abstract class DirectoryAggregateData<T> : EntryData<T> {
        private static readonly EnumerationOptions EnumerationOptions = new EnumerationOptions {
            AttributesToSkip = 0,
            IgnoreInaccessible = true,
            MaxRecursionDepth = int.MaxValue,
            RecurseSubdirectories = true,
            ReturnSpecialDirectories = false
        };

        protected abstract T Seed { get; }

        protected abstract Task<T> Aggregate(T accumulate, FileSystemInfo info, CancellationToken cancellationToken);

        protected sealed override async Task<T> Access(CancellationToken cancellationToken) {
            var directory = Directory;
            if (directory == null) {
                return default;
            }
            var value = Seed;
            Set(value);
            try {
                var enumeration = directory.EnumerateFileSystemInfos(searchPattern: "*", EnumerationOptions);
                var collection = enumeration.CollectAsync(cancellationToken);
                var flatten = collection.FlattenAsync(cancellationToken);
                await foreach (var info in flatten) {
                    value = await Aggregate(value, info, cancellationToken);
                    Set(value);
                }
            }
            catch {
                Set(default);
                throw;
            }
            return Value;
        }

        protected sealed override void Refresh() {
            Set(default);
        }

        public DirectoryInfo Directory { get; }

        public DirectoryAggregateData(string key, DirectoryInfo directory, CancellationToken cancellationToken) : base(key, cancellationToken) {
            Directory = directory;
        }
    }
}
