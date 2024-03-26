using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class EntryStreamSource : IEntryStreamSource {
        protected Stream Stream { get; set; }
        protected IEntry Entry { get; }

        protected EntryStreamSource(IEntry entry) {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }

        protected virtual Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
        }

        public string EntryID => Entry.ID;
        public string EntryName => Entry.Name;

        public abstract long StreamLength { get; }
        public abstract string RelativePath { get; }

        public virtual string SourceFile => null;
        public virtual string SourceDirectory => null;

        Stream IEntryStreamSource.Stream =>
            Stream;

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
