using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Threading.Tasks {
    public static class Async {
        private class ThreadPoolWork : IThreadPoolWork {
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

        public static Task Run(CancellationToken cancellationToken, Action action) {
            if (null == action) throw new ArgumentNullException(nameof(action));
            if (cancellationToken.IsCancellationRequested) {
                return Task.FromCanceled(cancellationToken);
            }
            return Task.Run(() => action(), cancellationToken);
        }

        public static Task<T> Run<T>(CancellationToken cancellationToken, Func<T> function) {
            if (null == function) throw new ArgumentNullException(nameof(function));
            if (cancellationToken.IsCancellationRequested) {
                return Task.FromCanceled<T>(cancellationToken);
            }
            return Task.Run(() => function(), cancellationToken);
        }
    }
}
