using System;
using System.Collections.Generic;

namespace Domore.Collections.Generic {
    public static class CollectAsync {
        public static CollectAsyncOptions<TSource, TTransform> Options<TSource, TTransform>(IEnumerable<TSource> source, Func<TSource, TTransform> transform, long? ticks = null, CollectAsyncMode? mode = null, Func<TSource, bool> skip = null) {
            return new CollectAsyncOptions<TSource, TTransform> {
                Mode = mode ?? CollectAsyncOptions.DefaultMode,
                Skip = skip,
                Ticks = ticks ?? CollectAsyncOptions.DefaultTicks,
                Transform = transform
            };
        }

        public static CollectAsyncOptions<TSource> Options<TSource>(IEnumerable<TSource> source, long? ticks = null, CollectAsyncMode? mode = null, Func<TSource, bool> skip = null) {
            return Options(source, item => item, ticks, mode, skip);
        }
    }
}
