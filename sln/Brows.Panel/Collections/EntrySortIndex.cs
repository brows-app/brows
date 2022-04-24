namespace Brows.Collections {
    internal class EntrySortIndex {
        public string Key { get; }
        public int Multiplier { get; set; }

        public EntrySortIndex(string key) {
            Key = key;
        }
    }
}
