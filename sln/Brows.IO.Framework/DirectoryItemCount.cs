using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal class DirectoryItemCount : DirectoryAggregateData<long?> {
        protected sealed override long? Seed => 0;

        protected sealed override Task<long?> Aggregate(long? accumulate, FileSystemInfo info, CancellationToken cancellationToken) {
            switch (Kinds) {
                case DirectoryItemCountKinds.Any:
                case DirectoryItemCountKinds.Directory | DirectoryItemCountKinds.File:
                    accumulate = accumulate + 1;
                    break;
                case DirectoryItemCountKinds.Directory:
                    if (info is DirectoryInfo) {
                        accumulate = accumulate + 1;
                    }
                    break;
                case DirectoryItemCountKinds.File:
                    if (info is FileInfo) {
                        accumulate = accumulate + 1;
                    }
                    break;
            }
            return Task.FromResult(accumulate);
        }

        public DirectoryItemCountKinds Kinds { get; }

        public DirectoryItemCount(string key, DirectoryInfo directory, DirectoryItemCountKinds kinds, CancellationToken cancellationToken) : base(key, directory, cancellationToken) {
            Kinds = kinds;
        }

        public DirectoryItemCount(string key, DirectoryInfo directory, CancellationToken cancellationToken) : this(key, directory, DirectoryItemCountKinds.Any, cancellationToken) {
        }
    }
}
