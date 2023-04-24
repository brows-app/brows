namespace Brows.Config {
    internal class PropItem {
        public string Name { get; set; }
        public double? Width { get; set; }

        public string Key { get; }

        public PropItem(string key) {
            Key = key;
        }
    }
}
