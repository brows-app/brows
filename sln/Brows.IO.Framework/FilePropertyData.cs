using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal class FilePropertyData {
        private class Implementation : EntryData<object> {
            public FileSystemInfoWrapper Wrap { get; }

            public Implementation(string key, FileSystemInfoWrapper wrap, CancellationToken cancellationToken) : base(key, cancellationToken) {
                Wrap = wrap;
            }

            protected sealed override async Task<object> Access(CancellationToken cancellationToken) {
                return await Wrap.Get(Key);
            }

            protected sealed override void Refresh() {
                Wrap.RefreshingProperty = true;
            }
        }

        public string Key { get; }
        public double Width { get; }

        public FilePropertyData(string key, double width) {
            Key = key;
            Width = width;
        }

        public IEntryData Implement(FileSystemInfoWrapper wrap, CancellationToken cancellationToken) {
            return new Implementation(Key, wrap, cancellationToken);
        }
    }
}
