using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using IO;
    using Logger;

    internal sealed class DirectorySize : EntryData {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(DirectorySize)));
        private ILog _Log;

        protected sealed override async Task<object> Access() {
            var directory = Directory;
            if (directory == null) {
                return null;
            }
            var value = 0L;
            Set(value);
            await Log.Performance(directory.Name, async () => {
                try {
                    await foreach (var file in directory.RecurseFilesAsync(CancellationToken)) {
                        Set(value += file.Length);
                    }
                }
                catch {
                    Set(null);
                    throw;
                }
            });
            return Value;
        }

        public DirectoryInfo Directory { get; }
        public CancellationToken CancellationToken { get; }

        public DirectorySize(DirectoryInfo directory, CancellationToken cancellationToken) : base(nameof(DirectorySize)) {
            Directory = directory;
            CancellationToken = cancellationToken;
        }

        public sealed override void Refresh() {
            Set(null);
            base.Refresh();
        }
    }
}
