namespace Domore.Collections.Generic {
    public static class FlattenAsync {
        public static readonly FlattenAsyncOptions Immediately = new FlattenAsyncOptions {
            ChunkSize = 1,
            DelayMilliseconds = -1
        };
    }
}
