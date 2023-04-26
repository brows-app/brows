using Domore.Notification;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Brows {
    internal sealed class OperationCollection : Notifier, IOperationCollection {
        private readonly ObservableCollection<IOperation> List = new();

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
            var item = sender as IOperation;
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
        public object Source => List;
        public int Count => List.Count;

        public void Add(IOperation item) {
            if (null == item) throw new ArgumentNullException(nameof(item));
            if (item.Relevant) {
                Relevance++;
            }
            item.RelevantChanged += Item_RelevantChanged;
            List.Add(item);
        }

        public bool Remove(IOperation item) {
            if (null == item) throw new ArgumentNullException(nameof(item));
            if (item.Relevant) {
                Relevance--;
            }
            item.RelevantChanged -= Item_RelevantChanged;
            return List.Remove(item);
        }

        public void Clear() {
            var items = new List<IOperation>(List);
            foreach (var item in items) {
                Remove(item);
            }
        }

        IEnumerator<IOperation> IEnumerable<IOperation>.GetEnumerator() {
            return ((IEnumerable<IOperation>)List).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)List).GetEnumerator();
        }
    }
}
