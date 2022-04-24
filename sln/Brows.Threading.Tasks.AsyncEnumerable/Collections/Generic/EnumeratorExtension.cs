using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Collections.Generic {
    using Threading.Tasks;

    internal static class EnumeratorExtension {
        public static async Task<bool> MoveNextAsync<T>(this IEnumerator<T> enumerator, CancellationToken cancellationToken) {
            if (null == enumerator) throw new ArgumentNullException(nameof(enumerator));
            return await Async.Run(cancellationToken, enumerator.MoveNext);
        }

        public static async Task DisposeAsync<T>(this IEnumerator<T> enumerator) {
            if (null == enumerator) throw new ArgumentNullException(nameof(enumerator));
            await Task.Run(enumerator.Dispose);
        }
    }
}
