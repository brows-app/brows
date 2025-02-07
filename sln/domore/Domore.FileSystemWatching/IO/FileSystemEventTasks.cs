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

        private readonly List<Item> List = [];

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
                await eventLocker.WaitAsync();
            }
            catch (ObjectDisposedException) {
                return;
            }
            var tasks = List.Select(async item => {
                try {
                    var task = item.Task(e);
                    if (task != null) {
                        await task.ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (item.Canceled) {
                    if (Log.Debug()) {
                        Log.Debug("Canceled");
                    }
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
            });
            try {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(ex);
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

        private void Remove(Item item) {
            var list = List;
            var listCount = List.Count;
            lock (Set) {
                list.Remove(item);
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

        public static FileSystemEventTask Add(string path, Func<FileSystemEventArgs, CancellationToken, Task> task, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(task);
            var inst = default(FileSystemEventTasks);
            var item = Item.Create(task, token);
            lock (Set) {
                if (Set.TryGetValue(path, out inst) == false) {
                    Set[path] = inst = new FileSystemEventTasks(path);
                }
                inst.List.Add(item);
            }
            return new Disposable(inst, item);
        }

        private sealed class Disposable : FileSystemEventTask {
            public Item Item { get; }
            public FileSystemEventTasks Collection { get; }

            public Disposable(FileSystemEventTasks collection, Item item) {
                Collection = collection ?? throw new ArgumentNullException(nameof(collection));
                Item = item;
            }

            protected sealed override void Dispose(bool disposing) {
                if (disposing) {
                    Collection.Remove(Item);
                }
                base.Dispose(disposing);
            }
        }

        private sealed class Item {
            private CancellationToken CancellationToken { get; }
            private Func<FileSystemEventArgs, CancellationToken, Task> Factory { get; }

            private Item(Func<FileSystemEventArgs, CancellationToken, Task> factory, CancellationToken cancellationToken) {
                Factory = factory ?? throw new ArgumentNullException(nameof(factory));
                CancellationToken = cancellationToken;
            }

            public bool Canceled => CancellationToken.IsCancellationRequested;

            public Task Task(FileSystemEventArgs e) {
                return Factory(e, CancellationToken);
            }

            public static Item Create(Func<FileSystemEventArgs, CancellationToken, Task> factory, CancellationToken cancellationToken) {
                return new Item(factory, cancellationToken);
            }
        }
    }
}
