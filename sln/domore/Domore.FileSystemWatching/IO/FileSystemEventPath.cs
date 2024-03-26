using System.IO;
using System.Threading;

namespace Domore.IO {
    internal sealed class FileSystemEventPath {
        private FileSystemNotifier Notifier =>
            _Notifier ?? (
            _Notifier = new FileSystemNotifier(Path));
        private FileSystemNotifier _Notifier;

        private CancellationTokenSource Cancellation =>
            _Cancellation ?? (
            _Cancellation = new CancellationTokenSource());
        private CancellationTokenSource _Cancellation;

        public SynchronizationContext SynchronizationContext {
            get => Notifier.SynchronizationContext;
            set => Notifier.SynchronizationContext = value;
        }

        public object RemoveState { get; set; }
        public string Path { get; }

        public FileSystemEventPath(string path) {
            Path = path;
        }

        public void Add(FileSystemEventHandler handler) {
            Notifier.Read += handler;
            Notifier.Begin(Cancellation.Token);
        }

        public bool Remove(FileSystemEventHandler handler = null) {
            if (handler != null) {
                Notifier.Read -= handler;
            }
            return !Notifier.ReadHandler;
        }

        public void Removed() {
            try { Cancellation.Cancel(); } catch { }
            try { Cancellation.Dispose(); } catch { }
        }
    }
}
