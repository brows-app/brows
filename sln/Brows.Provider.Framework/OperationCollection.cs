using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Brows {
    internal sealed class OperationCollection : Notifier, IOperationCollection {
        private readonly ObservableCollection<Operation> Collection = new();

        private int Relevance {
            get => _Relevance;
            set {
                if (Change(ref _Relevance, value, nameof(Relevance))) {
                    NotifyPropertyChanged(nameof(Relevant));
                }
            }
        }
        private int _Relevance;

        private void Item_RelevantChanged(object sender, EventArgs e) {
            var item = sender as Operation;
            if (item != null) {
                if (item.Relevant) {
                    Relevance++;
                }
                else {
                    Relevance--;
                }
            }
        }

        public bool Relevant => Relevance > 0;
        public object Source => Collection;
        public int Count => Collection.Count;

        public void Add(Operation item) {
            if (item is null) throw new ArgumentNullException(nameof(item));
            if (item.Relevant) {
                Relevance++;
            }
            item.RelevantChanged += Item_RelevantChanged;
            Collection.Add(item);
        }

        public bool Remove(Operation item) {
            if (item == null) return false;
            if (item.Complete == false) return false;
            var removed = Collection.Remove(item);
            if (removed) {
                if (item.Relevant) {
                    Relevance--;
                }
            }
            item.RelevantChanged -= Item_RelevantChanged;
            return removed;
        }

        public IEnumerator<IOperation> GetEnumerator() {
            return Collection.GetEnumerator();
        }

        public IEnumerable<IOperation> AsEnumerable() {
            return Collection.AsEnumerable();
        }

        bool IOperationCollection.Remove(IOperation item) {
            return item is Operation operation
                ? Remove(operation)
                : false;
        }
    }
}
