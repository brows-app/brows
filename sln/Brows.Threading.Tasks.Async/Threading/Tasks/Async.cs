using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Threading.Tasks {
    public static class Async {
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
