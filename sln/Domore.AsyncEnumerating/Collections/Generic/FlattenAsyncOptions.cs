namespace Domore.Collections.Generic {
    public class FlattenAsyncOptions {
        public int ChunkSize { get; set; } = 100;
        public int DelayMilliseconds { get; set; } = 1;
        public int DelayAfterTicks { get; set; } = 100;
    }
}
