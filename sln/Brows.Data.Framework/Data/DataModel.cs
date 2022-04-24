namespace Brows.Data {
    using ComponentModel;

    public class DataModel : NotifyPropertyChanged, IDataModel {
        public virtual IDataStore Store =>
            _Store ?? (
            _Store = new DataStore(GetType()));
        private IDataStore _Store;
    }
}
