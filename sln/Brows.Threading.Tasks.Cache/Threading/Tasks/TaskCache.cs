using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Threading.Tasks {
    public class TaskCache<TResult> {
        private bool Cached;
        private Task<TResult> Task;

        public TResult Result { get; private set; }
        public Func<CancellationToken, Task<TResult>> Factory { get; }

        public TaskCache(Func<CancellationToken, Task<TResult>> factory) {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public async ValueTask<TResult> Ready(CancellationToken cancellationToken) {
            var result = Result;
            var cached = Cached;
            if (cached == false) {
                var task = Task;
                if (task == null) {
                    task = Task = Factory(cancellationToken);
                }
                try {
                    result = Result = await task;
                    cached = Cached = true;
                }
                catch {
                    Task = null;
                    Cached = false;
                    throw;
                }
            }
            return result;
        }
    }
}
