namespace Brows.Data {
    public interface IDataStore {
        void Save(object data);
        object Load();
    }
}
