using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Threading.Tasks {
    public static class Async {
        private struct ThreadPoolWork : IThreadPoolWork {
            public CancellationToken CancellationToken { get; }

            public ThreadPoolWork(CancellationToken cancellationToken) {
                CancellationToken = cancellationToken;
            }

            public Task Run(Action action) {
                if (CancellationToken.IsCancellationRequested) {
                    return Task.FromCanceled(CancellationToken);
                }
                return Task.Run(action, CancellationToken);
            }

            public Task<T> Run<T>(Func<T> func) {
                if (CancellationToken.IsCancellationRequested) {
                    return Task.FromCanceled<T>(CancellationToken);
                }
                return Task.Run(func, CancellationToken);
            }
        }

        public static IThreadPoolWork With(CancellationToken cancellationToken) {
            return new ThreadPoolWork(cancellationToken);
        }

        public static async Task Await(Task task) {
            if (task != null) {
                await task;
            }
            await Task.CompletedTask;
        }

        public static async Task<T> Await<T>(Task<T> task) {
            if (task != null) {
                return await task;
            }
            return await Task.FromResult<T>(default);
        }
    }
}
