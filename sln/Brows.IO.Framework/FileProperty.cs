namespace Brows {
    public class FileProperty : IFileProperty {
        public string Key { get; }
        public object Value { get; }

        public FileProperty(string key, object value) {
            Key = key;
            Value = value;
        }
    }
}
