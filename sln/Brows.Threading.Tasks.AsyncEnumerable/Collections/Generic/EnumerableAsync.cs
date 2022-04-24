using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Collections.Generic {
    using Threading.Tasks;

    public class EnumerableAsync {
        internal EnumerableAsync() {
        }

        public static EnumerableAsync<T> For<T>(Func<IEnumerable<T>> factory, EnumerableOptions options = null) {
            return new EnumerableAsync<T>(factory, options);
        }
    }

    public class EnumerableAsync<T> : EnumerableAsync {
        internal EnumerableAsync(Func<IEnumerable<T>> factory, EnumerableOptions options = null) {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Options = options ?? new EnumerableOptions();
        }

        public Func<IEnumerable<T>> Factory { get; }
        public EnumerableOptions Options { get; }

        public async IAsyncEnumerable<T> Enumerate([EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var enumerable = await Async.Run(cancellationToken, () => Factory());
            if (enumerable == null) yield break;

            var options = Options;
            var delay = options.Delay;
            var limit = delay?.Limit;
            var count = delay?.Limit;
            var threshold = delay?.Threshold;
            var milliseconds = delay?.Milliseconds;
            var mode = options.Mode;
            var e =
                mode == EnumerableMode.Default ? enumerable.Async(cancellationToken) :
                mode == EnumerableMode.Channel ? enumerable.Channel(options.ChannelAllowSynchronousContinuations, cancellationToken) :
                throw new NotImplementedException();
            await foreach (var item in e) {
                if (cancellationToken.IsCancellationRequested) {
                    break;
                }
                if (delay != null) {
                    if (--threshold <= 0) {
                        if (--count <= 0) {
                            await Task.Delay(milliseconds.Value, cancellationToken);
                            count = limit;
                        }
                    }
                }
                yield return item;
            }
        }
    }
}
