using Brows.Gui;
using Domore.Notification;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal abstract class EntryObservation : Notifier, IEntryObservation, IProviderFocus, ICommandSourceObject, IControlled<IEntryObservationController> {
        private static readonly PropertyChangedEventArgs CurrentEvent = new(nameof(Current));
        private static readonly PropertyChangedEventArgs ObservedEvent = new(nameof(Observed));
        private static readonly PropertyChangedEventArgs SelectedEvent = new(nameof(Selected));

        private readonly List<IEntry> SortingList = new();

        private void Controller_CurrentEntryChanged(object sender, EventArgs e) {
            CurrentChanged?.Invoke(this, e);
            NotifyPropertyChanged(CurrentEvent);
        }

        private void Collection_ObservationChanged(object sender, EventArgs e) {
            ObservedChanged?.Invoke(this, e);
            NotifyPropertyChanged(ObservedEvent);
        }

        private void Collection_SelectionChanged(object sender, EventArgs e) {
            SelectedChanged?.Invoke(this, e);
            NotifyPropertyChanged(SelectedEvent);
        }

        private void Controller_ManualInteraction(object sender, EventArgs e) {
            ManualInteraction = true;
        }

        private void Controller_NothingSelected(object sender, EventArgs e) {
            foreach (var entry in Collection.Observation) {
                entry.Select = false;
            }
        }

        private void Controller_DraggingSelected(object sender, EventArgs e) {
            var controller = sender as IEntryObservationController;
            if (controller != null) {
                Provider.DragSelected(controller.DraggingSource);
            }
        }

        internal abstract IEntry InternalLookupID(string value);
        internal abstract IEntry InternalLookupName(string value);

        protected EntryObservationSource Collection {
            get {
                if (_Collection == null) {
                    _Collection = new();
                    _Collection.SelectionChanged += Collection_SelectionChanged;
                    _Collection.ObservationChanged += Collection_ObservationChanged;
                }
                return _Collection;
            }
        }
        private EntryObservationSource _Collection;

        protected async Task Add(IEnumerable<IEntry> items, int count) {
            if (items == null) return;
            if (count == 0) return;
            var chunkSize = Provider.Config.Observe.Add.Size;
            var chunkDelay = Provider.Config.Observe.Add.Delay;
            var chunks = items.Chunk(chunkSize < 1 ? count : chunkSize);
            var chunkd = false;
            foreach (var chunk in chunks) {
                if (chunkd == false) {
                    chunkd = true;
                }
                else {
                    if (chunkDelay > 0) {
                        await Task.Delay(chunkDelay, Provider.Token);
                    }
                }
                var sorted = default(IEnumerable<Task>);
                var sorting = DataView.Sorting;
                if (sorting.Count > 0) {
                    var sortingTasks = chunk.Select(async item => await item.Ready(sorting)).ToList();
                    var sortingComplete = sortingTasks.All(task => task.IsCompleted);
                    if (sortingComplete == false) {
                        sorted = sortingTasks.Select(async task => {
                            var item = await task;
                            Collection.Add(item);
                            SortingList.Remove(item);
                            SortingChanged?.Invoke(this, EventArgs.Empty);
                        });
                        SortingList.AddRange(chunk);
                        SortingChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                if (sorted != null) {
                    await Task.WhenAll(sorted);
                }
                else {
                    Collection.Add(chunk);
                }
                ObservedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected async Task Remove(IEnumerable<IEntry> items, int count) {
            if (items == null) return;
            if (count == 0) return;
            var current = Controller?.CurrentEntry();
            var currentRemoved = false;
            var currentPosition = Controller?.CurrentPosition();
            var chunkSize = Provider.Config.Observe.Remove.Size;
            var chunkDelay = Provider.Config.Observe.Remove.Delay;
            var chunks = items.Chunk(chunkSize < 1 ? count : chunkSize);
            foreach (var chunk in chunks) {
                foreach (var item in chunk) {
                    var itemRemoved = Collection.Remove(item);
                    if (itemRemoved) {
                        var currentIsItem = current == item;
                        if (currentIsItem) {
                            currentRemoved = true;
                        }
                    }
                }
                if (chunkDelay > 0) {
                    await Task.Delay(chunkDelay, Provider.Token);
                }
                ObservedChanged?.Invoke(this, EventArgs.Empty);
            }
            if (currentRemoved) {
                if (currentPosition.HasValue) {
                    var controller = Controller;
                    if (controller != null) {
                        for (var cp = currentPosition.Value; cp >= 0; cp--) {
                            var breakNow = controller.CurrentPosition(cp);
                            if (breakNow) {
                                break;
                            }
                        }
                    }
                }
            }
        }

        public event EventHandler SortingChanged;
        public event EventHandler CurrentChanged;
        public event EventHandler SelectedChanged;
        public event EventHandler ObservedChanged;
        public event EventHandler ControllerChanged;

        public EntryDataView DataView =>
            _DataView ?? (
            _DataView = new EntryDataView(Provider, Collection));
        private EntryDataView _DataView;

        public object SortCommand =>
            _SortCommand ?? (
            _SortCommand = Request.Create(key => {
                var k = $"{key}";
                var sorting = DataView.Sorting;
                var direction = sorting.TryGetValue(k, out var d)
                    ? d
                    : null;
                direction =
                    direction == null ? EntrySortDirection.Ascending :
                    direction == EntrySortDirection.Ascending ? EntrySortDirection.Descending :
                    null;
                DataView.Sort(sorting.Change(k, direction));
            }));
        private object _SortCommand;

        public object Source =>
            Collection;

        public object Current =>
            Controller?.CurrentEntry();

        public IReadOnlyList<IEntry> Sorting =>
            SortingList;

        public IReadOnlySet<IEntry> Selected =>
            Collection.Selection;

        public IReadOnlyList<IEntry> Observed =>
            Collection.Observation;

        public IReadOnlyList<string> Keys =>
            DataView.Keys;

        public bool ManualInteraction {
            get => _ManualInteraction;
            private set => Change(ref _ManualInteraction, value, nameof(ManualInteraction));
        }
        private bool _ManualInteraction;

        public Provider Provider {
            get => _Provider ?? throw new InvalidOperationException();
            set => Change(ref _Provider, value, nameof(Provider));
        }
        private Provider _Provider;

        public bool Focus() {
            return Controller?.Focus() == true;
        }

        public virtual void End() {
            CurrentChanged = null;
            SelectedChanged = null;
            ObservedChanged = null;
            Collection.SelectionChanged -= Collection_SelectionChanged;
            Collection.ObservationChanged -= Collection_ObservationChanged;
            Collection.Clear();
            Collection.End();
        }

        public async Task Init(CancellationToken token) {
            await DataView.Init(token);
        }

        bool IProviderFocus.Set() {
            return Controller?.Focus() ?? false;
        }

        bool IProviderFocus.Get() {
            return Controller?.Focused() ?? false;
        }

        IEntryDataView IEntryObservation.DataView =>
            DataView;

        IEntryObservationController IControlled<IEntryObservationController>.Controller {
            set {
                var newValue = value;
                var oldValue = Controller;
                if (oldValue != newValue) {
                    if (oldValue != null) {
                        oldValue.DraggingSelected -= Controller_DraggingSelected;
                        oldValue.NothingSelected -= Controller_NothingSelected;
                        oldValue.ManualInteraction -= Controller_ManualInteraction;
                        oldValue.CurrentEntryChanged -= Controller_CurrentEntryChanged;
                    }
                    if (newValue != null) {
                        newValue.DraggingSelected += Controller_DraggingSelected;
                        newValue.NothingSelected += Controller_NothingSelected;
                        newValue.ManualInteraction += Controller_ManualInteraction;
                        newValue.CurrentEntryChanged += Controller_CurrentEntryChanged;
                    }
                    Controller = DataView.Controller = newValue;
                    ControllerChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        private IEntryObservationController Controller;

        IEntry IEntryObservation.Current() {
            return Controller?.CurrentEntry();
        }

        bool IEntryObservation.Current(IEntry entry) {
            return Controller?.CurrentEntry(entry) ?? false;
        }

        async Task<bool> IEntryObservation.Current(IEntry entry, CancellationToken token) {
            var controller = Controller;
            if (controller == null) {
                return false;
            }
            return await controller.CurrentEntry(entry, token);
        }

        IEntry IEntryObservation.LookupID(string value) {
            return InternalLookupID(value);
        }

        IEntry IEntryObservation.LookupName(string value) {
            return InternalLookupID(value);
        }

        object ICommandSourceObject.Instance =>
            this;

        IEnumerable ICommandSourceObject.Collection =>
            new[] { this };
    }

    internal sealed class EntryObservation<TEntry> : EntryObservation where TEntry : IEntry {
        private readonly List<TEntry> List;
        private readonly Dictionary<string, TEntry> SetID;
        private readonly Dictionary<string, TEntry> SetName;

        internal sealed override IEntry InternalLookupID(string value) {
            return LookupID(value);
        }

        internal sealed override IEntry InternalLookupName(string value) {
            return LookupName(value);
        }

        public int InitialCapacity { get; }
        public IEqualityComparer<string> CompareID { get; }
        public IEqualityComparer<string> CompareName { get; }

        public IReadOnlyList<TEntry> Items =>
            List;

        public EntryObservation(int initialCapacity, IEqualityComparer<string> compareID, IEqualityComparer<string> compareName) {
            List = new(InitialCapacity = initialCapacity);
            SetID = new(InitialCapacity, CompareID = compareID);
            SetName = new(InitialCapacity, CompareName = compareName);
        }

        public sealed override void End() {
            base.End();
            List.Clear();
            SetID.Clear();
            SetName.Clear();
        }

        public async Task Add(IReadOnlyCollection<TEntry> items) {
            if (items == null) return;
            if (items.Count == 0) return;
            foreach (var item in items) {
                if (item is not null) {
                    List.Add(item);
                    SetID[item.ID] = item;
                    SetName[item.Name] = item;
                }
            }
            await Add(items.Cast<IEntry>(), items.Count);
        }

        public async Task Remove(IReadOnlyCollection<TEntry> items) {
            if (items == null) return;
            if (items.Count == 0) return;
            if (items == List) {
                items = new List<TEntry>(items);
                List.Clear();
                SetID.Clear();
                SetName.Clear();
            }
            else {
                foreach (var item in items) {
                    if (item is not null) {
                        List.Remove(item);
                        SetID.Remove(item.ID);
                        SetName.Remove(item.Name);
                    }
                }
            }
            await Remove(items.Cast<IEntry>(), items.Count);
        }

        public TEntry LookupID(string value) {
            if (SetID.TryGetValue(value, out var item)) {
                return item;
            }
            return default;
        }

        public TEntry LookupName(string value) {
            if (SetName.TryGetValue(value, out var item)) {
                return item;
            }
            return default;
        }
    }
}
