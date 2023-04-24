using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Brows {
    internal sealed class OperationBaseCollection : IEnumerable<OperationBase> {
        private readonly ObservableCollection<OperationBase> Observable = new();

        public object Source =>
            Observable;

        public void Add(OperationBase item) {
            Observable.Add(item);
        }

        IEnumerator<OperationBase> IEnumerable<OperationBase>.GetEnumerator() {
            return ((IEnumerable<OperationBase>)Observable).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)Observable).GetEnumerator();
        }
    }
}
