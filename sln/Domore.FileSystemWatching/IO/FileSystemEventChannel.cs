using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Domore.IO {
    using Logs;

    internal class FileSystemEventChannel {
        private static readonly ILog Log = Logging.For(typeof(FileSystemEventChannel));

        public string Path { get; set; }
        public string Filter { get; set; }
        public bool? IncludeSubdirectories { get; set; }
        public int? InternalBufferSize { get; set; }
        public NotifyFilters? NotifyFilters { get; set; }

        public async IAsyncEnumerable<FileSystemEventArgs> Read([EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var path = Path;
            var channelOptions = new UnboundedChannelOptions { SingleReader = true, SingleWriter = false };
            var channel = Channel.CreateUnbounded<FileSystemEventArgs>(channelOptions);
            var reader = channel.Reader;
            var writer = channel.Writer;
            void disposedHandler(object sender, EventArgs e) {
                if (Log.Debug()) {
                    Log.Debug(nameof(FileSystemWatcher) + "[" + nameof(FileSystemWatcher.Disposed) + "][" + path + "]");
                }
                writer.TryComplete();
            }
            void errorHandler(object sender, ErrorEventArgs e) {
                if (Log.Debug()) {
                    Log.Debug(
                        nameof(FileSystemWatcher) + "[" + nameof(FileSystemWatcher.Error) + "][" + path + "]",
                        e?.GetException());
                }
                writer.TryComplete(e?.GetException());
            }
            async void eventHandler(object sender, FileSystemEventArgs e) {
                if (Log.Debug()) {
                    Log.Debug(nameof(FileSystemWatcher) + "[" + e?.ChangeType + "][" + e?.FullPath + "][" + path + "]");
                }
                try {
                    await writer.WriteAsync(e, cancellationToken);
                }
                catch (Exception ex) {
                    if (ex is OperationCanceledException canceled && canceled.CancellationToken == cancellationToken) {
                        writer.TryComplete();
                    }
                    else {
                        writer.TryComplete(ex);
                    }
                }
            }
            var watcher = await Task.Run(() => {
                FileSystemWatcher watcher = null;
                try {
                    watcher = new FileSystemWatcher();
                    watcher.Changed += eventHandler;
                    watcher.Created += eventHandler;
                    watcher.Deleted += eventHandler;
                    watcher.Renamed += eventHandler;
                    watcher.Error += errorHandler;
                    watcher.Disposed += disposedHandler;
                    watcher.Path = path;
                    watcher.Filter = Filter ?? "";
                    watcher.NotifyFilter = NotifyFilters ?? FileSystemNotify.All;
                    watcher.IncludeSubdirectories = IncludeSubdirectories ?? false;
                    watcher.InternalBufferSize = InternalBufferSize ?? 65536;
                    watcher.EnableRaisingEvents = true;
                    return watcher;
                }
                catch (Exception e1) {
                    var innerException = e1;
                    try {
                        watcher?.Dispose();
                    }
                    catch (Exception e2) {
                        innerException = new AggregateException(e1, e2);
                    }
                    throw new FileSystemWatcherInitializationException(nameof(FileSystemWatcherInitializationException), innerException);
                }
            });
            try {
                for (; ; ) {
                    var writing = await reader.WaitToReadAsync(cancellationToken);
                    if (writing == false) {
                        break;
                    }
                    if (reader.TryRead(out var e)) {
                        yield return e;
                    }
                }
            }
            finally {
                try {
                    watcher.Dispose();
                }
                catch {
                }
            }
        }
    }
}
