using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class EntryStreamSource : IEntryStreamSource {
        private readonly object Locker = new();
        private bool Streamed;

        private Stream Stream(bool @private) {
            if (Streamed == false) {
                lock (Locker) {
                    if (Streamed == false) {
                        Streamed = true;
                        return Stream();
                    }
                }
            }
            throw new EntryStreamSourceUsedException(this);
        }

        protected IEntry Entry { get; }

        protected EntryStreamSource(IEntry entry) {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }

        protected abstract Stream Stream();
        protected virtual Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
        }

        public string EntryID => Entry.ID;
        public string EntryName => Entry.Name;

        public abstract long StreamLength { get; }
        public abstract string RelativePath { get; }

        public virtual bool StreamValid => true;
        public virtual string SourceFile => null;

        Stream IEntryStreamSource.Stream() {
            return Stream(@private: true);
        }

        Task<IEntryStreamReady> IEntryStreamSource.StreamReady(CancellationToken token) {
            return StreamReady(token);
        }
    }

    public abstract class EntryStreamSource<TEntry> : EntryStreamSource where TEntry : IEntry {
        protected new TEntry Entry { get; }

        protected EntryStreamSource(TEntry entry) : base(entry) {
            Entry = entry;
        }
    }
}
