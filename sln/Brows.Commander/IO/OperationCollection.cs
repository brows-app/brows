using System;
using System.Collections.Generic;

namespace Brows.IO {
    using Collections.ObjectModel;

    internal class OperationCollection : CollectionSource<IOperation>, IOperationCollection {
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
    }
}
