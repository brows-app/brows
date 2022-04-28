using System;

namespace Brows.Data {
    using Files;

    public class DataStore : IDataStore {
        private string Name =>
            _Name ?? (
            _Name = DataType.Name);
        private string _Name;

        private string Path =>
            _Path ?? (
            _Path = DataPath.For(Name, Name, DataFile.Extension));
        private string _Path;

        private DataPath DataPath =>
            _DataPath ?? (
            _DataPath = new DataPath());
        private DataPath _DataPath;

        public Type DataType { get; }
        public DataFile DataFile { get; }

        public DataStore(Type dataType, DataFile dataFile) {
            DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
            DataFile = dataFile ?? throw new ArgumentNullException(nameof(dataFile));
        }

        public DataStore(Type dataType) : this(dataType, new XmlDataFile()) {
        }

        void IDataStore.Save(object data) {
            DataFile.Save(data, DataType, Path);
        }

        object IDataStore.Load() {
            return DataFile.Load(DataType, Path);
        }
    }
}
