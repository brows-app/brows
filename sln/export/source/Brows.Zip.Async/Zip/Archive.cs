using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Brows.Zip;

using Collections.Generic;

internal sealed class Archive : IDisposable {
    private const int NestedCopyBufferSize = 81920;

    private static readonly ILog Log = Logging.For(typeof(Archive));

    private DisposableStack Disposable { get; }

    private void Dispose(bool disposing) {
        if (disposing) {
            Disposable.Dispose();
        }
    }

    private static void CopyNestedEntry(
        ZipEntryValidationStream source,
        FileStream destination,
        IOperationProgress progress,
        CancellationToken cancellationToken) {
        /*
         * ZipArchive buffers a non-seekable input in memory. Stage nested ZIP data so large entries stay bounded.
         */
        var buffer = new byte[NestedCopyBufferSize];
        for (; ; ) {
            cancellationToken.ThrowIfCancellationRequested();
            var read = source.Read(buffer, 0, buffer.Length);
            if (read == 0) {
                break;
            }
            cancellationToken.ThrowIfCancellationRequested();
            destination.Write(buffer, 0, read);
            if (progress is not null) {
                progress.Change(addProgress: read);
            }
        }
        destination.Flush();
        cancellationToken.ThrowIfCancellationRequested();
        destination.Position = 0;
    }

    internal Archive(ZipArchive zip, DisposableStack disposable) {
        Zip = zip ?? throw new ArgumentNullException(nameof(zip));
        Disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
    }

    internal static Archive Open(
        string file,
        ZipArchiveNest nest,
        ZipArchiveMode mode,
        IOperationProgress progress,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(nest);
        var nesting = new Queue<string>(nest);
        var disposable = new DisposableStack();
        try {
            var staging = disposable.Push(new ArchiveUpdateStaging());
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
                if (nestedEntry is null) {
                    throw new NestedEntryNotFoundException();
                }
                Stream stream;
                if (mode == ZipArchiveMode.Read) {
                    var source = disposable.Push(new ZipEntryValidationStream(
                        nestedEntry.Open(),
                        nestedEntry.Length,
                        unchecked((uint)nestedEntry.Crc32)));
                    var staged = disposable.Push(staging.CreateSourceFile());
                    if (progress is not null) {
                        progress.Change(
                            addTarget: nestedEntry.Length,
                            data: nestedEntry.FullName,
                            kind: OperationProgressKind.FileSize);
                    }
                    CopyNestedEntry(source, staged, progress, cancellationToken);
                    stream = staged;
                }
                else {
                    stream = disposable.Push(nestedEntry.Open());
                }
                if (Log.Info()) {
                    Log.Info(nameof(nestedEntry.Open) + " > " + nestedEntry.FullName);
                }
                archive = disposable.Push(new ZipArchive(stream, mode));
            }
            return new Archive(archive, disposable);
        }
        catch {
            try {
                disposable.Dispose();
            }
            catch (Exception ex) {
                if (Log.Warn()) {
                    Log.Warn(ex);
                }
            }
            throw;
        }
    }

    internal static Archive Open(
        string file,
        ZipArchiveNest nest,
        ZipArchiveMode mode,
        CancellationToken cancellationToken) {
        return Open(file, nest, mode, progress: null, cancellationToken);
    }

    internal static Archive Open(
        ZipArchivePath path,
        ZipArchiveMode mode,
        IOperationProgress progress,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(path);
        return Open(path.FileName, path.Nest, mode, progress, cancellationToken);
    }

    public ZipArchive Zip { get; }

    public static Archive Open(ZipArchivePath path, ZipArchiveMode mode, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(path);
        return Open(path.FileName, path.Nest, mode, cancellationToken);
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~Archive() {
        Dispose(false);
    }

    private sealed class NestedEntryNotFoundException : Exception {
    }
}
