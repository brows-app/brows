using Domore.Logs;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using IO;

    internal sealed class DirectorySize : EntryData<long?> {
        private static readonly ILog Log = Logging.For(typeof(DirectorySize));

        protected sealed override void Refresh() {
            Set(null);
        }

        protected sealed override async Task<long?> Access(CancellationToken cancellationToken) {
            var directory = Directory;
            if (directory == null) {
                return null;
            }
            var value = 0L;
            Set(value);
            try {
                await foreach (var file in directory.RecurseFilesAsync(cancellationToken)) {
                    Set(value += await file.LengthAsync(cancellationToken));
                }
            }
            catch {
                Set(null);
                throw;
            }
            return Value;
        }

        public DirectoryInfo Directory { get; }

        public DirectorySize(string key, DirectoryInfo directory, CancellationToken cancellationToken) : base(key, cancellationToken) {
            Directory = directory;
        }
    }
}
