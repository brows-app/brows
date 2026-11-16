using Brows.Commands;
using Brows.Gui;
using Brows.Panels;
using Brows.Providers;
using Brows.Requests;
using Domore.Notification;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Entries;

internal abstract class EntryObservation : Notifier, IEntryObservation,
                                                     IProviderFocus,
                                                     ICommandSourceObject,
                                                     IControlled<IEntryObservationController> {
    private static readonly PropertyChangedEventArgs DropEvent = new(nameof(Drop));
    private static readonly PropertyChangedEventArgs CurrentEvent = new(nameof(Current));
    private static readonly PropertyChangedEventArgs ObservedEvent = new(nameof(Observed));
    private static readonly PropertyChangedEventArgs SelectedEvent = new(nameof(Selected));

    private readonly List<IEntry> SortingList = [];
    private readonly Lock Locker = new();
    private volatile bool Ended;

    /// <summary>
    /// Gets whether the observation was ended, so that it accepts no further change.
    /// </summary>
    private protected bool IsEnded =>
        Ended;

    /// <summary>
    /// Applies the specified change, unless the observation was already ended.
    /// </summary>
    /// <param name="change">
    /// The change to apply. It runs while the teardown of the observation is blocked, so that a
    /// change never races the end of the observation.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the change was applied.
    /// </returns>
    private protected bool TryChange(Action change) {
        lock (Locker) {
            if (Ended) {
                return false;
            }
            change();
            return true;
        }
    }

    private void Controller_CurrentEntryChanged(object sender, EventArgs e) {
        CurrentChanged?.Invoke(this, e);
        NotifyPropertyChanged(CurrentEvent);
    }

    private void Collection_DropChanged(object sender, EventArgs e) {
        DropChanged?.Invoke(this, e);
        NotifyPropertyChanged(DropEvent);
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
            if (field == null) {
                field = new();
                field.DropChanged += Collection_DropChanged;
                field.SelectionChanged += Collection_SelectionChanged;
            }
            return field;
        }
    }

    protected async Task Add(IEnumerable<IEntry> items, int count, CancellationToken token) {
        if (items == null) return;
        if (count == 0) return;
        if (IsEnded) {
            return;
        }
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
                    await Task.Delay(chunkDelay, token);
                }
            }
            if (IsEnded) {
                /*
                 * The observation was ended while the chunk was awaited, so the chunk is dropped
                 * instead of being added to an observation that no longer accepts changes.
                 */
                return;
            }
            var sorted = default(IEnumerable<Task>);
            var sorting = DataView.Sorting;
            if (sorting.Count > 0) {
                var sortingTasks = chunk.Select(async item => await item.Ready(sorting)).ToList();
                var sortingComplete = sortingTasks.All(task => task.IsCompleted);
                if (sortingComplete == false) {
                    sorted = sortingTasks.Select(async task => {
                        var item = await task;
                        TryChange(() => {
                            Collection.Add(item);
                            SortingList.Remove(item);
                            SortingChanged?.Invoke(this, EventArgs.Empty);
                        });
                    });
                    SortingList.AddRange(chunk);
                    SortingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            if (sorted != null) {
                await Task.WhenAll(sorted);
            }
            else {
                TryChange(() => Collection.Add(chunk));
            }
            if (IsEnded) {
                return;
            }
            ObservedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected async Task Remove(IEnumerable<IEntry> items, int count, CancellationToken token) {
        if (items == null) return;
        if (count == 0) return;
        if (IsEnded) {
            return;
        }
        var current = Controller?.CurrentEntry();
        var currentRemoved = false;
        var currentPosition = Controller?.CurrentPosition();
        Exception error = null;
        var chunkSize = Provider.Config.Observe.Remove.Size;
        var chunkDelay = Provider.Config.Observe.Remove.Delay;
        var chunks = items.Chunk(chunkSize < 1 ? count : chunkSize);
        foreach (var chunk in chunks) {
            foreach (var item in chunk) {
                var itemRemoved = false;
                try {
                    TryChange(() => {
                        itemRemoved = Collection.Remove(item);
                    });
                    var itemIsNotObserved = Collection.Observation.Contains(item) == false;
                    var wasRemovedFromSource = itemRemoved == false && itemIsNotObserved;
                    if (wasRemovedFromSource) {
                        itemRemoved = true;
                    }
                }
                catch (Exception ex) {
                    error ??= ex;
                    var itemIsNotObserved = Collection.Observation.Contains(item) == false;
                    itemRemoved = itemIsNotObserved;
                }
                if (itemRemoved) {
                    var currentIsItem = current == item;
                    if (currentIsItem) {
                        currentRemoved = true;
                    }
                }
            }
            if (chunkDelay > 0) {
                await Task.Delay(chunkDelay, token);
            }
            if (IsEnded) {
                return;
            }
            try {
                ObservedChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) {
                error ??= ex;
            }
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
        if (error is not null) {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    public event EventHandler DropChanged;
    public event EventHandler SortingChanged;
    public event EventHandler CurrentChanged;
    public event EventHandler SelectedChanged;
    public event EventHandler ObservedChanged;
    public event EventHandler ControllerChanged;

    public EntryDataView DataView => field ??=
        new(Provider, Collection);

    public object SortCommand => field ??=
        Provider?.Import?.Find<IRequestFactory>()?.Create(
            owner: this,
            canExecute: _ => true,
            dependencies: [],
            execute: key => {
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
            });

    public object Source =>
        Collection;

    public object Current =>
        Controller?.CurrentEntry();

    public IReadOnlyList<IEntry> Sorting =>
        SortingList;

    public IPanelDrop Drop =>
        Collection.Drop;

    public IReadOnlySet<IEntry> Selected =>
        Collection.Selection;

    public IReadOnlyList<IEntry> Observed =>
        Collection.Observation;

    public IReadOnlyList<string> Keys =>
        DataView.Keys;

    public bool ManualInteraction {
        get;
        private set => Change(ref field, value);
    }

    public EntryProvider Provider {
        get => field ?? throw new InvalidOperationException();
        set => Change(ref field, value);
    }

    public bool Focus() {
        return Controller?.Focus() == true;
    }

    public virtual void End() {
        lock (Locker) {
            if (Ended) {
                return;
            }
            Ended = true;
        }
        Exception error = null;
        CurrentChanged = null;
        DropChanged = null;
        SortingChanged = null;
        SelectedChanged = null;
        ObservedChanged = null;
        ControllerChanged = null;
        Collection.DropChanged -= Collection_DropChanged;
        Collection.SelectionChanged -= Collection_SelectionChanged;
        try {
            Collection.Clear();
        }
        catch (Exception ex) {
            error = ex;
        }
        try {
            Collection.End();
        }
        catch (Exception ex) {
            error ??= ex;
        }
        if (error is not null) {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    public Task Init(CancellationToken token) {
        return DataView.Init(token);
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
        return InternalLookupName(value);
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

    public EntryObservation(int initialCapacity,
                            IEqualityComparer<string> compareID,
                            IEqualityComparer<string> compareName) {
        List = new(InitialCapacity = initialCapacity);
        SetID = new(InitialCapacity, CompareID = compareID);
        SetName = new(InitialCapacity, CompareName = compareName);
    }

    public sealed override void End() {
        Exception error = null;
        try {
            base.End();
        }
        catch (Exception ex) {
            error = ex;
        }
        finally {
            List.Clear();
            SetID.Clear();
            SetName.Clear();
        }
        if (error is not null) {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    public Task Add(IReadOnlyCollection<TEntry> items, CancellationToken token) {
        if (items == null) return Task.CompletedTask;
        if (items.Count == 0) return Task.CompletedTask;
        if (IsEnded) {
            return Task.CompletedTask;
        }
        TryChange(() => {
            foreach (var item in items) {
                if (item is not null) {
                    List.Add(item);
                    SetID[item.ID] = item;
                    SetName[item.Name] = item;
                }
            }
        });
        if (IsEnded) {
            return Task.CompletedTask;
        }
        return Add(items.Cast<IEntry>(), items.Count, token);
    }

    public Task Remove(IReadOnlyCollection<TEntry> items, CancellationToken token) {
        if (items == null) return Task.CompletedTask;
        if (items.Count == 0) return Task.CompletedTask;
        if (IsEnded) {
            return Task.CompletedTask;
        }
        TryChange(() => {
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
        });
        if (IsEnded) {
            return Task.CompletedTask;
        }
        return Remove(items.Cast<IEntry>(), items.Count, token);
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
