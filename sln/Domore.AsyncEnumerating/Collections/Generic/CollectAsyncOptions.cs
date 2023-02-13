using System;

namespace Domore.Collections.Generic {
    public class CollectAsyncOptions {
        internal const long DefaultTicks = 100;
        internal const CollectAsyncMode DefaultMode = CollectAsyncMode.Channel;

        public long Ticks { get; set; } = DefaultTicks;
        public CollectAsyncMode Mode { get; set; } = DefaultMode;
    }

    public class CollectAsyncOptions<TSource> : CollectAsyncOptions {
        public Func<TSource, bool> Skip {
            get => _Skip ?? (_Skip = _ => false);
            set => _Skip = value;
        }
        private Func<TSource, bool> _Skip;
    }

    public class CollectAsyncOptions<TSource, TTransform> : CollectAsyncOptions<TSource> {
        public Func<TSource, TTransform> Transform {
            get => _Transform ?? (_Transform = source => source is TTransform transform ? transform : default);
            set => _Transform = value;
        }
        private Func<TSource, TTransform> _Transform;
    }
}
