using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Brows.Collections.ObjectModel {
    using ComponentModel;

    public abstract class CollectionSource : NotifyPropertyChanged, IEnumerable {
        private readonly ICollection Collection;

        private CollectionSource(ICollection collection) {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        public int Count =>
            Collection.Count;

        public object Source =>
            Collection;

        IEnumerator IEnumerable.GetEnumerator() {
            return Collection.GetEnumerator();
        }

        public class Of<T> : CollectionSource, IReadOnlyList<T> {
            private readonly new ObservableCollection<T> Collection;

            private void Collection_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                OnPropertyChanged(e);
            }

            private Of(ObservableCollection<T> collection) : base(collection) {
                Collection = collection ?? throw new ArgumentNullException(nameof(collection));
                ((INotifyPropertyChanged)Collection).PropertyChanged += Collection_PropertyChanged;
            }

            protected IList<T> List =>
                Collection;

            public T this[int index] =>
                Collection[index];

            public Of() : this(new ObservableCollection<T>()) {
            }

            IEnumerator<T> IEnumerable<T>.GetEnumerator() {
                return ((IEnumerable<T>)Collection).GetEnumerator();
            }
        }
    }

    public class CollectionSource<T> : CollectionSource.Of<T> {
    }
}
