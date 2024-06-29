using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.IO {
    public sealed class FileSystemEventTasks {
        private static readonly ILog Log = Logging.For(typeof(FileSystemEventTasks));
        private static readonly Dictionary<string, FileSystemEventTasks> Set = [];

        private readonly List<Func<FileSystemEventArgs, Task>> List = [];

        private FileSystemEvent Event;
        private SemaphoreSlim EventLocker;

        private string Path { get; }

        private FileSystemEventTasks(string path) {
            Path = path;
            Event = new FileSystemEvent(Path);
            Event.Handler += Event_Handler;
            EventLocker = new SemaphoreSlim(1, 1);
        }

        private async void Event_Handler(object sender, FileSystemEventArgs e) {
            var eventLocker = EventLocker;
            if (eventLocker == null) {
                return;
            }
            try {
                await eventLocker.WaitAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException) {
                return;
            }
            try {
                await Task.WhenAll(List.Select(task => task(e))).ConfigureAwait(false);
            }
            catch (Exception ex) {
                if (ex is OperationCanceledException canceled) {
                    if (Log.Debug()) {
                        Log.Debug(nameof(OperationCanceledException));
                    }
                }
                else {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
            }
            finally {
                try {
                    eventLocker.Release();
                }
                catch (ObjectDisposedException) {
                }
            }
        }

        private void Remove(Func<FileSystemEventArgs, Task> task) {
            var list = List;
            var listCount = List.Count;
            lock (Set) {
                list.Remove(task);
                listCount = List.Count;
                if (listCount == 0) {
                    Set.Remove(Path);
                }
            }
            if (listCount == 0) {
                using (EventLocker) {
                    Event.Handler -= Event_Handler;
                    Event = null;
                    EventLocker = null;
                }
            }
        }

        public static FileSystemEventTask Add(string path, Func<FileSystemEventArgs, Task> task) {
            ArgumentNullException.ThrowIfNull(task);
            var inst = default(FileSystemEventTasks);
            lock (Set) {
                if (Set.TryGetValue(path, out inst) == false) {
                    Set[path] = inst = new FileSystemEventTasks(path);
                }
                inst.List.Add(task);
            }
            return new Disposable(inst, task);
        }

        private sealed class Disposable : FileSystemEventTask {
            public Func<FileSystemEventArgs, Task> Task { get; }
            public FileSystemEventTasks Instance { get; }

            public Disposable(FileSystemEventTasks instance, Func<FileSystemEventArgs, Task> task) {
                Instance = instance ?? throw new ArgumentNullException(nameof(instance));
                Task = task;
            }

            protected sealed override void Dispose(bool disposing) {
                if (disposing) {
                    Instance.Remove(Task);
                }
                base.Dispose(disposing);
            }
        }
    }
}
