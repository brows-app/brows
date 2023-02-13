namespace Brows.Config {
    internal class FindConfig {
        public int ChunkSize { get; set; } = 1;
        public int CollectAfterTicks { get; set; } = 100;
        public int DelayAfterTicks { get; set; } = 1000;
        public int DelayMilliseconds { get; set; } = 1;
    }
}
