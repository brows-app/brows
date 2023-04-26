using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.Threading.Tasks {
    public sealed class TaskCache<TResult> {
        private readonly object Locker = new();

        private bool Cached;
        private Task<TResult> Task;

        public TResult Result { get; private set; }
        public Func<CancellationToken, Task<TResult>> Factory { get; }

        public TaskCache(Func<CancellationToken, Task<TResult>> factory) {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public async ValueTask<TResult> Ready(CancellationToken token) {
            if (Cached == false) {
                if (Task == null) {
                    lock (Locker) {
                        if (Task == null) {
                            Task = Factory(token);
                        }
                    }
                }
                try {
                    Result = await Task;
                }
                catch {
                    lock (Locker) {
                        Task = null;
                    }
                    throw;
                }
            }
            Cached = true;
            return Result;
        }

        public async Task<TResult> Refreshed(CancellationToken token) {
            Refresh();
            return await Ready(token);
        }

        public void Refresh() {
            lock (Locker) {
                Task = null;
                Cached = false;
                Result = default;
            }
        }
    }
}
