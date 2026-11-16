using Domore.Logs;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Brows;

internal sealed class ZipStreamSet : EntryStreamSet {
    private static readonly ILog Log = Logging.For(typeof(ZipStreamSet));

    private static void LogCleanupException(Exception ex) {
        try {
            if (Log.Error()) {
                Log.Error(ex);
            }
        }
        catch {
        }
    }

    private sealed class SourceReady : EntryStreamReady {
        private readonly IEntryStreamSource[] Sources;

        private int Disposed;

        protected override void Dispose(bool disposing) {
            var alreadyDisposed = Interlocked.Exchange(ref Disposed, 1) != 0;
            if (alreadyDisposed == false) {
                foreach (var source in Sources) {
                    if (source is ZipStreamSource zipSource) {
                        try {
                            zipSource.ReleaseArchivePathReference();
                        }
                        catch (Exception ex) {
                            LogCleanupException(ex);
                        }
                    }
                }
            }
            base.Dispose(disposing);
        }

        public SourceReady(IEnumerable<IEntryStreamSource> sources) {
            Sources = sources.ToArray();
        }
    }

    protected override IEnumerable<IEntryStreamSource> StreamSource() {
        return Collection;
    }

    protected override IEntryStreamReady StreamSourceReady() {
        return new SourceReady(Collection);
    }

    public IReadOnlyList<IEntryStreamSource> Collection { get; }

    public ZipStreamSet(IEnumerable<IEntryStreamSource> collection) {
        Collection = collection?.ToArray() ?? Array.Empty<IEntryStreamSource>();
    }
}
