using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading;

namespace Brows.Zip {
    using Collections.Generic;

    internal sealed class Archive : IDisposable {
        private static readonly ILog Log = Logging.For(typeof(Archive));

        private DisposableStack Disposable { get; }

        private Archive(ZipArchive zip, DisposableStack disposable) {
            Zip = zip ?? throw new ArgumentNullException(nameof(zip));
            Disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
        }

        private void Dispose(bool disposing) {
            if (disposing) {
                Disposable.Dispose();
            }
        }

        public ZipArchive Zip { get; }

        public static Archive Open(ZipArchivePath path, ZipArchiveMode mode, CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(path);
            var file = path.FileName;
            var nesting = new Queue<string>(path.Nest);
            var disposable = new DisposableStack();
            try {
                if (Log.Info()) {
                    Log.Info(nameof(ZipFile.Open) + " > " + file);
                }
                var archive = disposable.Push(ZipFile.Open(file, mode));
                for (; ; ) {
                    if (cancellationToken.IsCancellationRequested) {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    var dequeue = nesting.TryDequeue(out var nested);
                    if (dequeue == false) {
                        break;
                    }
                    var nestedEntry = archive.GetEntry(nested);
                    if (nestedEntry == null) {
                        throw new NestedEntryNotFoundException();
                    }
                    var stream = disposable.Push(nestedEntry.Open());
                    if (Log.Info()) {
                        Log.Info(nameof(nestedEntry.Open) + " > " + nestedEntry.FullName);
                    }
                    archive = disposable.Push(new ZipArchive(stream, mode));
                }
                return new Archive(archive, disposable);
            }
            catch {
                using (disposable) {
                    throw;
                }
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Archive() {
            Dispose(false);
        }

        private class NestedEntryNotFoundException : Exception {
        }
    }
}
