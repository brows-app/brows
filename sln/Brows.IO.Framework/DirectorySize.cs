using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Threading.Tasks;

    internal sealed class DirectorySize : DirectoryAggregateData<long?> {
        protected sealed override long? Seed => 0;

        protected sealed override async Task<long?> Aggregate(long? accumulate, FileSystemInfo info, CancellationToken cancellationToken) {
            var length = info is FileInfo file
                ? await Async.With(cancellationToken).Run(() => file.Length)
                : 0;
            return accumulate + length;
        }

        public DirectorySize(string key, DirectoryInfo directory, CancellationToken cancellationToken) : base(key, directory, cancellationToken) {
        }
    }
}
