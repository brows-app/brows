using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Threading.Tasks {
    public static class Async {
        public static async Task Run(CancellationToken cancellationToken, Action action) {
            if (null == action) throw new ArgumentNullException(nameof(action));
            await Task.Run(() => {
                if (cancellationToken.IsCancellationRequested) {
                    cancellationToken.ThrowIfCancellationRequested();
                    // TODO: Log debug before throwing.
                }
                action();
            }, cancellationToken);
        }

        public static async Task<T> Run<T>(CancellationToken cancellationToken, Func<T> function) {
            if (null == function) throw new ArgumentNullException(nameof(function));
            return await Task.Run(() => {
                if (cancellationToken.IsCancellationRequested) {
                    cancellationToken.ThrowIfCancellationRequested();
                    // TODO: Log debug before throwing.
                }
                return function();
            }, cancellationToken);
        }
    }
}
