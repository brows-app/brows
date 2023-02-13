using System;
using System.Collections.Generic;
using System.Threading;

namespace Domore.Collections.Generic {
    using CollectAsyncModes;

    public static class CollectAsyncExtension {
        public static IAsyncEnumerable<IReadOnlyList<TTransform>> CollectAsync<TSource, TTransform>(this IEnumerable<TSource> source, CollectAsyncOptions<TSource, TTransform> options, CancellationToken cancellationToken) {
            options = options ?? new CollectAsyncOptions<TSource, TTransform>();
            switch (options.Mode) {
                case CollectAsyncMode.Channel:
                    return Channel.CollectAsync(source, options, cancellationToken);
                default:
                    throw new NotImplementedException();
            }
        }

        public static IAsyncEnumerable<IReadOnlyList<TSource>> CollectAsync<TSource>(this IEnumerable<TSource> source, CollectAsyncOptions<TSource> options, CancellationToken cancellationToken) {
            if (options != null) {
                options = new CollectAsyncOptions<TSource, TSource> {
                    Skip = options.Skip,
                    Ticks = options.Ticks,
                    Transform = item => item
                };
            }
            return CollectAsync(
                source: source,
                options: options as CollectAsyncOptions<TSource, TSource>,
                cancellationToken: cancellationToken);
        }

        public static IAsyncEnumerable<IReadOnlyList<TSource>> CollectAsync<TSource>(this IEnumerable<TSource> source, CancellationToken cancellationToken) {
            return CollectAsync(source, new CollectAsyncOptions<TSource>(), cancellationToken);
        }
    }
}
