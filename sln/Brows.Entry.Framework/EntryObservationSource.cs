using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Brows {
    internal sealed class EntryObservationSource : Notifier {
        private readonly HashSet<IEntry> Select = new();

        private ObservableCollection<IEntry> Collection {
            get {
                if (_Collection == null) {
                    _Collection = new();
                    _Collection.CollectionChanged += Collection_CollectionChanged;
                }
                return _Collection;
            }
        }
        private ObservableCollection<IEntry> _Collection;

        private void SelectChanged() {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void CollectionChanged() {
            ObservationChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Collection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            CollectionChanged();
        }

        private void Entry_Selected(object sender, EntrySelectedEventArgs e) {
            Selected(sender as IEntry);
        }

        private void Selected(IEntry entry) {
            if (entry != null) {
                if (entry.Select) {
                    if (Select.Add(entry)) {
                        SelectChanged();
                    }
                }
                else {
                    if (Select.Remove(entry)) {
                        SelectChanged();
                    }
                }
            }
        }

        private void Initialize(IEntry entry) {
            if (null == entry) throw new ArgumentNullException(nameof(entry));
            entry.Selected += Entry_Selected;
            Selected(entry);
        }

        private void Finalize(IEntry entry) {
            if (null == entry) throw new ArgumentNullException(nameof(entry));
            if (Select.Remove(entry)) {
                SelectChanged();
            }
            entry.Selected -= Entry_Selected;
        }

        public event EventHandler SelectionChanged;
        public event EventHandler ObservationChanged;

        public object Items =>
            Collection;

        public IReadOnlyList<IEntry> Observation =>
            Collection;

        public IReadOnlySet<IEntry> Selection =>
            Select;

        public void Add(IEntry entry) {
            Collection.Add(entry);
            Initialize(entry);
        }

        public void Add(IEnumerable<IEntry> entries) {
            if (null == entries) throw new ArgumentNullException(nameof(entries));
            foreach (var entry in entries) {
                Add(entry);
            }
        }

        public bool Remove(IEntry entry) {
            Finalize(entry);
            return Collection.Remove(entry);
        }

        public void Clear() {
            foreach (var entry in Collection) {
                Finalize(entry);
            }
            Collection.Clear();
        }

        public void End() {
            SelectionChanged = null;
            ObservationChanged = null;
            Collection.CollectionChanged -= Collection_CollectionChanged;
        }
    }
}
