namespace Brows {
    using Collections.ObjectModel;

    internal class OperationCollection : CollectionSource<OperationBase> {
        public void Add(OperationBase item) => List.Add(item);
    }
}
