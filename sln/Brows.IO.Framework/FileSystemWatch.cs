using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using NOTIFYFILTERS = System.IO.NotifyFilters;

namespace Brows {
    using Threading.Tasks;

    internal class FileSystemWatch {
        private const NOTIFYFILTERS NotifyFiltersAll =
            NOTIFYFILTERS.FileName |
            NOTIFYFILTERS.DirectoryName |
            NOTIFYFILTERS.Attributes |
            NOTIFYFILTERS.Size |
            NOTIFYFILTERS.LastWrite |
            NOTIFYFILTERS.LastAccess |
            NOTIFYFILTERS.CreationTime |
            NOTIFYFILTERS.Security;

        public string Path { get; set; }
        public string Filter { get; set; }
        public int? InternalBufferSize { get; set; }
        public NOTIFYFILTERS? NotifyFilters { get; set; }

        public async IAsyncEnumerable<FileSystemEventArgs> ChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var channelOptions = new UnboundedChannelOptions { SingleReader = true, SingleWriter = false };
            var channel = Channel.CreateUnbounded<FileSystemEventArgs>(channelOptions);
            var reader = channel.Reader;
            var writer = channel.Writer;

            using var watcher = new FileSystemWatcher();
            watcher.Changed += watcherHandler;
            watcher.Created += watcherHandler;
            watcher.Deleted += watcherHandler;
            watcher.Renamed += watcherHandler;
            watcher.Error += watcherError;
            await Async.Run(cancellationToken, () => watcher.Path = Path);
            await Async.Run(cancellationToken, () => watcher.Filter = Filter ?? "");
            await Async.Run(cancellationToken, () => watcher.NotifyFilter = NotifyFilters ?? NotifyFiltersAll);
            await Async.Run(cancellationToken, () => watcher.InternalBufferSize = InternalBufferSize ?? 65536);
            await Async.Run(cancellationToken, () => watcher.EnableRaisingEvents = true);

            async void watcherHandler(object sender, FileSystemEventArgs e) {
                try {
                    await writer.WriteAsync(e, cancellationToken);
                }
                catch (OperationCanceledException) {
                }
                catch (Exception ex) {
                    writer.TryComplete(ex);
                }
            }

            void watcherError(object sender, ErrorEventArgs e) {
                writer.TryComplete(e?.GetException());
            }

            for (; ; ) {
                var writing = await reader.WaitToReadAsync(cancellationToken);
                if (writing == false) break;
                if (reader.TryRead(out var e)) {
                    yield return e;
                }
            }
        }
    }
}
