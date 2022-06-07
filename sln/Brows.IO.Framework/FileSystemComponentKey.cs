namespace Brows {
    internal class FileSystemComponentKey : IComponentResourceKey {
        public string For(string key) {
            return nameof(FileSystemEntryData) + "_" + key;
        }
    }
}
