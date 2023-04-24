using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.IO {
    using Logs;

    public class FileSystemNotifier {
        private static readonly ILog Log = Logging.For(typeof(FileSystemNotifier));
        private readonly object Locker = new();
        private readonly FileSystemEventChannel Channel;

        private async Task<Exception> RunOnce(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info($"{nameof(RunOnce)}[{Path}]");
            }
            var error = default(Exception);
            try {
                await foreach (var change in Channel.Read(cancellationToken)) {
                    var e = new FileSystemNotifyEventArgs(change);
                    void invoke(object _) {
                        Read?.Invoke(this, e.FileSystemEvent);
                        Notify?.Invoke(this, e);
                    }
                    var context = SynchronizationContext;
                    if (context != null) {
                        context.Send(invoke, null);
                    }
                    else {
                        invoke(null);
                    }
                    if (e.Break) {
                        if (Log.Info()) {
                            Log.Info($"{nameof(e.Break)}[{e.Break}][{Path}]");
                        }
                        break;
                    }
                }
            }
            catch (FileSystemWatcherInitializationException) {
                throw;
            }
            catch (Exception ex) {
                error = ex;
            }
            if (error != null) {
                var directoryExists = await Task.Run(() => Directory.Exists(Path), cancellationToken);
                if (directoryExists == false) {
                    if (Log.Info()) {
                        Log.Info($"{nameof(Directory.Exists)}[{directoryExists}][{Path}]");
                    }
                    error = null;
                }
            }
            return error;
        }

        internal event FileSystemEventHandler Read;
        internal bool ReadHandler => Read != null;

        public event FileSystemNotifyEventHandler Notify;

        public SynchronizationContext SynchronizationContext { get; set; }
        public bool Running { get; private set; }
        public string Path => Channel.Path;
        public string Filter => Channel.Filter;
        public bool? IncludeSubdirectories => Channel.IncludeSubdirectories;
        public int? InternalBufferSize => Channel.InternalBufferSize;
        public NotifyFilters? NotifyFilters => Channel.NotifyFilters;

        public FileSystemNotifier(string path, string filter = null, NotifyFilters? notifyFilters = null, bool? includeSubdirectories = null, int? internalBufferSize = null) {
            Channel = new FileSystemEventChannel {
                Filter = filter,
                IncludeSubdirectories = includeSubdirectories,
                InternalBufferSize = internalBufferSize,
                NotifyFilters = notifyFilters,
                Path = path
            };
        }

        public async Task<Exception> Run(CancellationToken cancellationToken) {
            if (Running) {
                throw new FileSystemNotifierRunningException();
            }
            lock (Locker) {
                if (Running) {
                    throw new FileSystemNotifierRunningException();
                }
                Running = true;
            }
            try {
                for (; ; ) {
                    var error = await RunOnce(cancellationToken);
                    if (error is null) {
                        return null;
                    }
                    if (error is OperationCanceledException canceled && canceled.CancellationToken == cancellationToken) {
                        if (Log.Info()) {
                            Log.Info($"{nameof(OperationCanceledException)}[{Path}]");
                        }
                        return null;
                    }
                    if (Log.Warn()) {
                        Log.Warn($"{nameof(Path)}[{Path}]", error);
                    }
                }
            }
            finally {
                Running = false;
            }
        }

        public bool TryRun(CancellationToken cancellationToken, out Task<Exception> running) {
            if (Running == false) {
                lock (Locker) {
                    if (Running == false) {
                        running = Run(cancellationToken);
                        return true;
                    }
                }
            }
            running = null;
            return false;
        }

        public bool Begin(CancellationToken cancellationToken) {
            var run = TryRun(cancellationToken, out var running);
            if (run) {
                running.ContinueWith(task => {
                    if (Log.Info()) {
                        Log.Info($"{nameof(task.IsCompleted)}[{Path}]");
                    }
                    var error = task.IsCompletedSuccessfully ? task.Result : task.Exception;
                    if (error != null) {
                        if (Log.Error()) {
                            Log.Error($"{nameof(Path)}[{Path}]", error);
                        }
                    }
                });
            }
            return run;
        }

        public bool Begin() {
            return Begin(CancellationToken.None);
        }
    }
}
