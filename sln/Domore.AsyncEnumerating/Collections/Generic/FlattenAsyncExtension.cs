using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.Collections.Generic {
    public static class FlattenAsyncExtension {
        private static async ValueTask<long> Delay(long now, int ticks, int milliseconds, CancellationToken cancellationToken) {
            var tc = Environment.TickCount64;
            if (tc - now > ticks) {
                switch (milliseconds) {
                    case 0:
                        await Task.Yield();
                        break;
                    case > 0:
                        await Task.Delay(milliseconds, cancellationToken);
                        break;
                }
                return Environment.TickCount64;
            }
            return now;
        }

        public static IAsyncEnumerable<TSource> FlattenAsync<TSource>(this IAsyncEnumerable<IReadOnlyList<TSource>> source, CancellationToken cancellationToken) {
            return FlattenAsync(source, null, cancellationToken);
        }

        public static async IAsyncEnumerable<TSource> FlattenAsync<TSource>(this IAsyncEnumerable<IReadOnlyList<TSource>> source, FlattenAsyncOptions options, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (null == source) throw new ArgumentNullException(nameof(source));
            var now = Environment.TickCount64;
            var opt = options ?? new FlattenAsyncOptions();
            await foreach (var group in source) {
                var ticks = opt.DelayAfterTicks;
                var delay = opt.DelayMilliseconds;
                var count = group.Count;
                switch (count) {
                    case 0:
                        now = await Delay(now, ticks, delay, cancellationToken);
                        break;
                    case 1:
                        yield return group[0];
                        now = await Delay(now, ticks, delay, cancellationToken);
                        break;
                    default:
                        var chunkSize = opt.ChunkSize;
                        if (chunkSize < 1) {
                            chunkSize = count;
                        }
                        if (chunkSize == 1) {
                            foreach (var item in group) {
                                yield return item;
                            }
                            now = await Delay(now, ticks, delay, cancellationToken);
                        }
                        else {
                            foreach (var chunk in group.Chunk(chunkSize)) {
                                foreach (var item in chunk) {
                                    yield return item;
                                }
                                now = await Delay(now, ticks, delay, cancellationToken);
                            }
                        }
                        break;
                }
            }
        }
    }
}
