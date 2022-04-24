using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using IO;
    using Logger;
    using Threading.Tasks;

    internal class FileSystemNotifier {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(FileSystemNotifier)));
        private ILog _Log;

        private FileSystemWatch Watch =>
            _Watch ?? (
            _Watch = new FileSystemWatch());
        private FileSystemWatch _Watch;

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<FileSystemNotifier>());
        private TaskHandler _TaskHandler;

        private void Notify(FileSystemEventArgs e) {
            OnNotified(e);
        }

        private async Task<Exception> RunOnceAsync(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(RunOnceAsync));
            }
            var next = false;
            var error = default(Exception);
            var changes = Watch.ChangesAsync(cancellationToken);
            await using (var enumerator = changes.GetAsyncEnumerator(cancellationToken)) {
                for (; ; ) {
                    try {
                        next = false;
                        next = await enumerator.MoveNextAsync();
                    }
                    catch (OperationCanceledException ex) {
                        if (Log.Info()) {
                            Log.Info(
                                Path,
                                ex?.GetType()?.Name ?? nameof(OperationCanceledException));
                        }
                    }
                    catch (Exception ex) {
                        error = ex;
                    }
                    if (error != null) {
                        var exists = await DirectoryAsync.Exists(Path, cancellationToken);
                        if (exists == false) {
                            return null;
                        }
                        if (Log.Warn()) {
                            Log.Warn(
                                Path,
                                error);
                        }
                        return error;
                    }
                    if (next == false) {
                        return null;
                    }
                    Notify(enumerator.Current);
                }
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken) {
            for (; ; ) {
                var error = await RunOnceAsync(cancellationToken);
                if (error == null) break;
            }
        }

        protected virtual void OnNotified(FileSystemEventArgs e) {
            Notified?.Invoke(this, e);
        }

        public event FileSystemEventHandler Notified;

        public string Path {
            get => Watch.Path;
            set => Watch.Path = value;
        }

        public string Filter {
            get => Watch.Filter;
            set => Watch.Filter = value;
        }

        public int? InternalBufferSize {
            get => Watch.InternalBufferSize;
            set => Watch.InternalBufferSize = value;
        }

        public NotifyFilters? NotifyFilters {
            get => Watch.NotifyFilters;
            set => Watch.NotifyFilters = value;
        }

        public void Begin(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(Begin));
            }
            TaskHandler.Begin(RunAsync(cancellationToken));
        }
    }
}
