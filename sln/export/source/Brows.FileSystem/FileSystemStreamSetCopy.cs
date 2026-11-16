using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class FileSystemStreamSetCopy : IDisposable {
    private static readonly ILog Log = Logging.For(typeof(FileSystemStreamSetCopy));

    private readonly Lock Locker = new();
    private readonly List<Task> Copies = [];
    private readonly Dictionary<string, TaskCompletionSource<IOperationProgress>> Directories =
        new(StringComparer.Ordinal);
    private readonly TaskCompletionSource Listed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource Linked;
    private readonly SemaphoreSlim Slots;
    private ExceptionDispatchInfo Failure;
    private int FilesPublished;
    private int DirectoriesCreated;
    private int SourcesFailed;
    private int SourcesCanceled;

    private sealed class Slot {
        private readonly SemaphoreSlim Semaphore;
        private int Released;

        internal Slot(SemaphoreSlim semaphore) {
            Semaphore = semaphore ?? throw new ArgumentNullException(nameof(semaphore));
        }

        internal void Release() {
            var releasing = Interlocked.Exchange(ref Released, 1) == 0;
            if (releasing) {
                Semaphore.Release();
            }
        }
    }

    private string Root { get; }
    private IOperationProgress Progress { get; }
    private CancellationToken Token { get; }

    private static string Normalize(string relativePath) {
        return relativePath.Replace('\\', '/').TrimEnd('/');
    }

    private static string ParentKey(string key) {
        var separator = key.LastIndexOf('/');
        return separator < 0
            ? null
            : key[..separator];
    }

    private static async Task Copy(Stream source,
                                   FileStream destination,
                                   long length,
                                   IOperationProgress progress,
                                   CancellationToken token) {
        if (length < 0) {
            try {
                length = source.Length;
            }
            catch (NotSupportedException) {
                length = -1;
            }
        }
        var lengthIsKnown = length >= 0;
        progress.Change(
            setProgress: 0,
            setTarget: Math.Max(0, length),
            kind: OperationProgressKind.FileSize);
        var buffer = new byte[81920];
        for (; ; ) {
            token.ThrowIfCancellationRequested();
            var read = await source.ReadAsync(buffer.AsMemory(), token);
            if (read == 0) {
                break;
            }
            await destination.WriteAsync(buffer.AsMemory(0, read), token);
            progress.Change(addProgress: read, addTarget: lengthIsKnown ? null : read);
        }
    }

    private void Fail(Exception error) {
        if (Log.Error()) {
            Log.Error(Log.Join("Copy stopped by an unexpected failure", Root), error);
        }
        lock (Locker) {
            Failure ??= ExceptionDispatchInfo.Capture(error);
            Succeeded = false;
        }
        try {
            Linked.Cancel();
        }
        catch (ObjectDisposedException) {
        }
    }

    private void Unsucceed() {
        lock (Locker) {
            Succeeded = false;
        }
    }

    private void Publish() {
        lock (Locker) {
            Copied = true;
        }
    }

    private async Task<(IOperationProgress Progress, string Key)> Parent(string key, CancellationToken token) {
        if (Progress is null) {
            return (null, null);
        }
        for (var parentKey = ParentKey(key); parentKey is not null; parentKey = ParentKey(parentKey)) {
            TaskCompletionSource<IOperationProgress> directory;
            lock (Locker) {
                Directories.TryGetValue(parentKey, out directory);
            }
            if (directory is null) {
                continue;
            }
            var directoryProgress = await directory.Task.WaitAsync(token);
            if (directoryProgress is not null) {
                return (directoryProgress, parentKey);
            }
        }
        return (Progress, null);
    }

    /*
     * Copies a single source and publishes it to its final location as soon as it is complete, so the
     * entries that finished before a cancellation or a sibling's failure are kept.
     * Returns true when the source is a directory.
     */
    private async Task<bool> Transfer(IEntryStreamSource source,
                                      string relativePath,
                                      IOperationProgress progress,
                                      CancellationToken token) {
        IEntryStreamReady sourceReady = null;
        Stream sourceStream = null;
        var sourceIsDirectory = false;
        string stagingPath = null;
        FileStream stagingStream = null;
        var length = 0L;
        try {
            try {
                sourceReady = await source.StreamReady(token);
                sourceStream = source.Stream;
                if (sourceStream is null) {
                    if (source.SourceDirectory is null) {
                        throw new InvalidDataException("The selected source has no file or directory content.");
                    }
                    sourceIsDirectory = true;
                }
                else {
                    var staging = await Task.Run(
                        () => FileSystemOutputPath.CreateStagingFile(Root, relativePath),
                        token);
                    stagingPath = staging.Path;
                    stagingStream = staging.Stream;
                    if (progress is null) {
                        await sourceStream.CopyToAsync(stagingStream, token);
                    }
                    else {
                        await Copy(sourceStream, stagingStream, source.StreamLength, progress, token);
                    }
                    length = stagingStream.Length;
                    await stagingStream.DisposeAsync();
                    stagingStream = null;
                }
            }
            finally {
                try {
                    if (stagingStream is not null) {
                        await stagingStream.DisposeAsync();
                    }
                }
                finally {
                    try {
                        var streamToDispose = sourceStream ?? source.Stream;
                        if (streamToDispose is not null) {
                            await streamToDispose.DisposeAsync();
                        }
                    }
                    finally {
                        sourceReady?.Dispose();
                    }
                }
            }
            if (sourceIsDirectory) {
                await Task.Run(() => FileSystemOutputPath.CreateDirectory(Root, relativePath), token);
                Interlocked.Increment(ref DirectoriesCreated);
                if (Log.Info()) {
                    Log.Info(Log.Join("Copy created directory", relativePath));
                }
                return true;
            }

            /*
             * The download is complete, so publishing is not canceled. A cancellation requested now
             * keeps the finished file instead of discarding it.
             */
            var publishedPath = await Task.Run(
                () => FileSystemOutputPath.PublishStagingFile(Root, relativePath, stagingPath));
            stagingPath = null;
            Interlocked.Increment(ref FilesPublished);
            if (Log.Info()) {
                Log.Info(Log.Join("Copy published file", publishedPath, length));
            }
            return false;
        }
        catch (Exception error) {
            var stagingWasCreated = stagingPath is not null;
            try {
                if (stagingStream is not null) {
                    await stagingStream.DisposeAsync();
                }
            }
            finally {
                if (stagingWasCreated) {
                    FileSystemOutputPath.DeleteStagingFile(Root, stagingPath);
                }
            }
            LogSourceFailure(relativePath, stagingWasCreated, error, token);
            throw;
        }
    }

    private void LogSourceFailure(string relativePath,
                                  bool stagingWasRemoved,
                                  Exception error,
                                  CancellationToken token) {
        if (token.IsCancellationRequested) {
            Interlocked.Increment(ref SourcesCanceled);
            if (Log.Info()) {
                Log.Info(Log.Join("Copy canceled", relativePath, "incomplete file removed", stagingWasRemoved));
            }
            return;
        }
        Interlocked.Increment(ref SourcesFailed);
        if (Log.Warn()) {
            Log.Warn(Log.Join(
                "Copy failed",
                relativePath,
                "incomplete file removed",
                stagingWasRemoved,
                error.GetType().Name,
                error.Message));
        }
    }

    private async Task CopySource(IEntryStreamSource source,
                                  string relativePath,
                                  Slot slot,
                                  TaskCompletionSource<IOperationProgress> directory,
                                  IOperationProgress progress,
                                  CancellationToken token) {
        var isDirectory = await Transfer(source, relativePath, progress, token);
        Publish();
        slot.Release();
        if (isDirectory is false) {
            return;
        }
        directory?.TrySetResult(progress);
        if (progress is not null) {
            /*
             * Children can only be registered while this delegate runs. The directory stays open
             * until every source is listed, so the entries listed inside it become its children.
             */
            await Listed.Task;
        }
    }

    private async Task Start(IEntryStreamSource source,
                             string relativePath,
                             string name,
                             IOperationProgress parent,
                             Slot slot,
                             TaskCompletionSource<IOperationProgress> directory,
                             CancellationToken token) {
        try {
            if (parent is null) {
                await CopySource(source, relativePath, slot, directory, null, token);
                return;
            }
            var result = await parent.Child(
                name,
                (progress, childToken) => CopySource(source, relativePath, slot, directory, progress, childToken),
                OperationFailureBehavior.Record,
                token);
            if (result.Kind != OperationResultKind.Succeeded) {
                Unsucceed();
            }
        }
        catch (Exception error) {
            var canceled = error is OperationCanceledException && token.IsCancellationRequested;
            if (canceled) {
                Unsucceed();
                return;
            }
            Fail(error);
        }
        finally {
            slot.Release();
            directory?.TrySetResult(null);
        }
    }

    private async Task List(IEntryStreamSet set, CancellationToken token) {
        var sources = set.StreamSource(token);
        if (sources is null) {
            return;
        }
        await foreach (var source in sources.WithCancellation(token)) {
            token.ThrowIfCancellationRequested();
            if (source is null) {
                continue;
            }
            Progress?.Change(data: source.EntryName);
            var relativePath = source.RelativePath;
            FileSystemOutputPath.ValidateRelativePath(relativePath);
            var key = Normalize(relativePath);
            var (parent, parentKey) = await Parent(key, token);
            var name = parentKey is null
                ? relativePath
                : key[(parentKey.Length + 1)..];
            var directory = default(TaskCompletionSource<IOperationProgress>);
            if (source.SourceDirectory is not null) {
                directory = new(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (Locker) {
                    Directories[key] = directory;
                }
            }
            if (Slots.CurrentCount == 0) {
                if (Log.Debug()) {
                    Log.Debug(Log.Join("Copy waiting for a slot", relativePath));
                }
            }
            await Slots.WaitAsync(token);
            var copy = Start(source, relativePath, name, parent, new Slot(Slots), directory, token);
            lock (Locker) {
                Copies.Add(copy);
            }
        }
    }

    internal int Concurrency { get; }
    internal bool Copied { get; private set; }
    internal bool Succeeded { get; private set; } = true;

    internal FileSystemStreamSetCopy(string root,
                                     IOperationProgress progress,
                                     int concurrency,
                                     CancellationToken token) {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Concurrency = concurrency;
        Progress = progress;
        Token = token;
        Linked = CancellationTokenSource.CreateLinkedTokenSource(token);
        Slots = new SemaphoreSlim(Math.Max(1, Concurrency));
    }

    internal async Task Work(IEntryStreamSet set) {
        ArgumentNullException.ThrowIfNull(set);
        if (Log.Info()) {
            Log.Info(Log.Join(
                "Copy starting",
                Root,
                "concurrency",
                Concurrency,
                "effective",
                Math.Max(1, Concurrency)));
        }
        var stopwatch = Stopwatch.StartNew();
        var listError = default(Exception);
        var listErrorIsInduced = false;
        try {
            await List(set, Linked.Token);
        }
        catch (Exception error) {
            listError = error;
            listErrorIsInduced = error is OperationCanceledException && Linked.IsCancellationRequested;
            var listErrorIsLogged = listErrorIsInduced is false && Token.IsCancellationRequested is false;
            if (listErrorIsLogged) {
                if (Log.Warn()) {
                    Log.Warn(Log.Join("Copy stopped listing sources", Root, error.GetType().Name, error.Message));
                }
            }
            Linked.Cancel();
        }
        finally {
            Listed.TrySetResult();
        }
        Task[] copies;
        lock (Locker) {
            copies = [.. Copies];
        }
        await Task.WhenAll(copies);
        if (Log.Info()) {
            Log.Info(Log.Join(
                "Copy finished",
                Root,
                "files", Volatile.Read(ref FilesPublished),
                "directories", Volatile.Read(ref DirectoriesCreated),
                "failed", Volatile.Read(ref SourcesFailed),
                "canceled", Volatile.Read(ref SourcesCanceled),
                "requested", Token.IsCancellationRequested,
                "elapsed", stopwatch.Elapsed));
        }
        Token.ThrowIfCancellationRequested();
        var listErrorIsOwn = listError is not null && listErrorIsInduced is false;
        if (listErrorIsOwn) {
            ExceptionDispatchInfo.Throw(listError);
        }
        Failure?.Throw();
        if (listError is not null) {
            ExceptionDispatchInfo.Throw(listError);
        }
    }

    public void Dispose() {
        Linked.Dispose();
        Slots.Dispose();
    }
}
