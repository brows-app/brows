namespace Brows {
    internal class FileSystemComponentKey : IComponentResourceKey {
        public object For(string key) {
            return nameof(FileSystemEntryData) + "_" + key;
        }
    }
}
