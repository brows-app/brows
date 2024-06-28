using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Brows {
    internal sealed class EntryObservationSource : Notifier {
        private readonly HashSet<IEntry> Select = [];
        private readonly ObservableCollection<IEntry> Collection = [];

        private void SelectChanged() {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Entry_Drop(object sender, EntryDropEventArgs e) {
            if (e != null) {
                Drop = e.Drop;
                Drop = null;
            }
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
            entry.Drop += Entry_Drop;
            entry.Selected += Entry_Selected;
            Selected(entry);
        }

        private void Finalize(IEntry entry) {
            if (null == entry) throw new ArgumentNullException(nameof(entry));
            if (Select.Remove(entry)) {
                SelectChanged();
            }
            if (Drop == entry) {
                Drop = null;
            }
            entry.Drop -= Entry_Drop;
            entry.Selected -= Entry_Selected;
        }

        private void PrivateAdd(IEntry entry) {
            Collection.Add(entry);
            Initialize(entry);
        }

        public event EventHandler DropChanged;
        public event EventHandler SelectionChanged;

        public object Items =>
            Collection;

        public IPanelDrop Drop {
            get => _Drop;
            private set {
                if (Change(ref _Drop, value, nameof(Drop))) {
                    DropChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        private IPanelDrop _Drop;

        public IReadOnlyList<IEntry> Observation =>
            Collection;

        public IReadOnlySet<IEntry> Selection =>
            Select;

        public void Add(IEntry entry) {
            PrivateAdd(entry);
        }

        public void Add(params IEntry[] entries) {
            ArgumentNullException.ThrowIfNull(entries);
            foreach (var entry in entries) {
                PrivateAdd(entry);
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
            DropChanged = null;
            SelectionChanged = null;
        }
    }
}
