using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CHANNEL = System.Threading.Channels.Channel;

namespace Brows.Collections.Generic {
    using Threading.Tasks;
    using ASYNC = Threading.Tasks.Async;

    internal static class EnumerableExtension {
        private static readonly TaskHandler TaskHandler = new TaskHandler(typeof(EnumerableExtension));

        private static async Task<IEnumerator<T>> GetEnumeratorAsync<T>(this IEnumerable<T> enumerable, CancellationToken cancellationToken) {
            if (null == enumerable) throw new ArgumentNullException(nameof(enumerable));
            return await ASYNC.Run(cancellationToken, enumerable.GetEnumerator);
        }

        public static async IAsyncEnumerable<T> Async<T>(this IEnumerable<T> enumerable, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            if (null == enumerable) throw new ArgumentNullException(nameof(enumerable));
            var enumerator = await enumerable.GetEnumeratorAsync(cancellationToken);
            try {
                for (; ; ) {
                    if (await enumerator.MoveNextAsync(cancellationToken)) {
                        yield return enumerator.Current;
                    }
                    else {
                        break;
                    }
                }
            }
            finally {
                await enumerator.DisposeAsync();
            }
        }

        public static async IAsyncEnumerable<T> Channel<T>(this IEnumerable<T> enumerable, bool allowSynchronousContinuations, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var channel = CHANNEL.CreateUnbounded<T>(new UnboundedChannelOptions {
                AllowSynchronousContinuations = allowSynchronousContinuations,
                SingleReader = true,
                SingleWriter = true
            });
            TaskHandler.Begin(Task.Run(cancellationToken: cancellationToken, action: async () => {
                var error = default(Exception);
                var writer = channel.Writer;
                try {
                    foreach (var item in enumerable) {
                        await writer.WriteAsync(item, cancellationToken);
                    }
                }
                catch (OperationCanceledException) {
                }
                catch (Exception ex) {
                    error = ex;
                }
                finally {
                    writer.Complete(error);
                }
            }));

            var reader = channel.Reader;
            for (; ; ) {
                var read = await reader.WaitToReadAsync(cancellationToken);
                if (read && reader.TryRead(out var item)) {
                    yield return item;
                }
                else {
                    break;
                }
            }
        }
    }
}
