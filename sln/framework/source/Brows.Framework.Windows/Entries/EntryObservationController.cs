using Domore.Logs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace Brows.Entries;

internal sealed class EntryObservationController : IEntryObservationController {
    private static readonly ILog Log = Logging.For(typeof(EntryObservationController));

    private EntryListView EntryListView;
    private EntryListViewItemsListView _ItemsListView;
    private IComparer CustomSort;
    private Point? DragStart;

    private List<IEntry> SelectedEntries {
        get => field ??= [];
        set;
    }

    private Dictionary<string, List<EntryGridViewColumnProxy>> GridColumns => field ??= [];

    private ListView ListView => _ItemsListView;
    private GridView GridView => _ItemsListView?.View as GridView;
    private ListCollectionView ListCollectionView =>
        (ListCollectionView)CollectionViewSource.GetDefaultView(ListView.ItemsSource);

    private bool SetCurrentEntry(IEntry item, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(Log.Join(nameof(SetCurrentEntry), item?.ID));
        }
        if (item == null) {
            return false;
        }
        ListView.ScrollIntoView(item);
        if (token.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();
        }
        if (Log.Debug()) {
            Log.Debug(Log.Join(nameof(ListView.ScrollIntoView), item.ID));
        }
        var moved = ListView.Items.MoveCurrentTo(item);
        if (Log.Debug()) {
            Log.Debug(Log.Join(nameof(ListView.Items.MoveCurrentTo), moved, item.ID));
        }
        if (moved == false) {
            return false;
        }
        if (token.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();
        }
        var control = ListView.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
        if (Log.Debug()) {
            Log.Debug(Log.Join(nameof(ListView.ItemContainerGenerator.ContainerFromItem), control, item.ID));
        }
        if (control == null) {
            return false;
        }
        if (token.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();
        }
        var focus = control.Focus();
        if (Log.Debug()) {
            Log.Debug(Log.Join(nameof(control.Focus), focus, item.ID));
        }
        if (focus == false) {
            return false;
        }
        return true;
    }

    private List<EntryGridViewColumnProxy> GridColumnList(string key) {
        if (GridColumns.TryGetValue(key, out var list) == false) {
            GridColumns[key] = list = new List<EntryGridViewColumnProxy>();
        }
        return list;
    }

    private void ListView_ItemsSourceChanged(object sender, EventArgs e) {
        UpdateListViewSource();
        Element.UpdateController();
    }

    private void UpdateListViewSource() {
        if (ListView is null || ListView.ItemsSource is null) {
            return;
        }
        var view = ListCollectionView;
        if (view != null) {
            view.CustomSort = CustomSort;
        }
    }

    private void ListView_ItemsListViewChanged(object sender, EventArgs e) {
        var oldListView = _ItemsListView;
        if (oldListView is not null) {
            oldListView.ItemsSourceChanged -= ListView_ItemsSourceChanged;
            oldListView.MouseLeave -= ListView_MouseLeave;
            oldListView.MouseMove -= ListView_MouseMove;
            oldListView.MouseUp -= ListView_MouseUp;
            oldListView.PreviewKeyDown -= ListView_PreviewKeyDown;
            oldListView.PreviewMouseDown -= ListView_PreviewMouseDown;
            oldListView.SelectionChanged -= ListView_SelectionChanged;
            oldListView.Items.CurrentChanged -= Items_CurrentChanged;
        }
        _ItemsListView = EntryListView?.ItemsListView;
        if (_ItemsListView is null) {
            Element.UpdateController();
            return;
        }
        _ItemsListView.ItemsSourceChanged += ListView_ItemsSourceChanged;
        _ItemsListView.MouseLeave += ListView_MouseLeave;
        _ItemsListView.MouseMove += ListView_MouseMove;
        _ItemsListView.MouseUp += ListView_MouseUp;
        _ItemsListView.PreviewKeyDown += ListView_PreviewKeyDown;
        _ItemsListView.PreviewMouseDown += ListView_PreviewMouseDown;
        _ItemsListView.SelectionChanged += ListView_SelectionChanged;
        _ItemsListView.Items.CurrentChanged += Items_CurrentChanged;
        UpdateListViewSource();
        Element.UpdateController(refresh: true);
    }

    private void ListView_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
        DragStart = e.ChangedButton == MouseButton.Left
            ? e.GetPosition(ListView)
            : null;
        ManualInteraction?.Invoke(this, e);
    }

    private void ListView_MouseLeave(object sender, MouseEventArgs e) {
        DragStart = null;
    }

    private void ListView_PreviewKeyDown(object sender, KeyEventArgs e) {
        ManualInteraction?.Invoke(this, e);
    }

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        var newlySelected = ListView.SelectedItems.Cast<IEntry>().ToList();
        var previouslySelected = SelectedEntries;
        var deselected = previouslySelected.Except(newlySelected);
        foreach (var entry in deselected) {
            entry.Select = false;
        }
        foreach (var entry in newlySelected) {
            entry.Select = true;
        }
        SelectedEntries = newlySelected;
    }

    private void ListView_MouseMove(object sender, MouseEventArgs e) {
        if (e is null) {
            return;
        }
        if (e.LeftButton != MouseButtonState.Pressed) {
            DragStart = null;
            return;
        }
        if (DragStart is not Point start) {
            return;
        }
        /*
         * WPF raises MouseMove without any pointer movement when the element
         * under the pointer changes, such as when a click selects an entry.
         * Start a drag only after the pointer has moved past the system's
         * drag threshold from where the button was pressed.
         */
        var position = e.GetPosition(ListView);
        var movedFarEnough =
            Math.Abs(position.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(position.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;
        if (movedFarEnough == false) {
            return;
        }
        var source = EntryFrom(e.OriginalSource as DependencyObject);
        if (source is null || source.Select == false) {
            return;
        }
        DragStart = null;
        DraggingSelected?.Invoke(this, e);
    }

    private void ListView_MouseUp(object sender, MouseButtonEventArgs e) {
        if (e != null) {
            switch (e.ChangedButton) {
                case MouseButton.Left:
                case MouseButton.Right:
                    var source = EntryFrom(e.OriginalSource as DependencyObject);
                    var isEntry = source is not null;
                    var nothing = !isEntry;
                    if (nothing) {
                        NothingSelected?.Invoke(this, e);
                    }
                    break;
            }
        }
    }

    private IEntry EntryFrom(DependencyObject source) {
        if (source is null || ListView is null) {
            return null;
        }
        var container = ItemsControl.ContainerFromElement(ListView, source) as ListViewItem;
        return container is null
            ? null
            : ListView.ItemContainerGenerator.ItemFromContainer(container) as IEntry;
    }

    private void Items_CurrentChanged(object sender, EventArgs e) {
        CurrentEntryChanged?.Invoke(this, e);
    }

    public event EventHandler NothingSelected;
    public event EventHandler ManualInteraction;
    public event EventHandler CurrentEntryChanged;
    public event EventHandler DraggingSelected;

    public EntryObservationControl Element { get; }

    public EntryObservationController(EntryObservationControl element) {
        Element = element ?? throw new ArgumentNullException(nameof(element));
    }

    internal void ListViewChanged(EntryListView oldListView, EntryListView newListView) {
        if (oldListView is not null) {
            oldListView.ItemsListViewChanged -= ListView_ItemsListViewChanged;
        }
        EntryListView = newListView;
        if (EntryListView is not null) {
            EntryListView.ItemsListViewChanged += ListView_ItemsListViewChanged;
        }
        ListView_ItemsListViewChanged(EntryListView, EventArgs.Empty);
    }

    object IEntryObservationController.DraggingSource => ListView;

    bool IEntryObservationController.AutoFit {
        set => _ItemsListView?.Sizing.SetAutoFit(value);
    }

    async Task<bool> IEntryObservationController.CurrentEntry(IEntry item, CancellationToken token) {
        if (token.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();
        }
        var result = false;
        await ListView.Dispatcher.BeginInvoke(priority: DispatcherPriority.Render, args: null, method: () => {
            try {
                result = SetCurrentEntry(item, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
            }
        });
        if (token.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();
        }
        return result;
    }

    bool IEntryObservationController.CurrentEntry(IEntry item) {
        return SetCurrentEntry(item, CancellationToken.None);
    }

    IEntry IEntryObservationController.CurrentEntry() {
        return ListView.Items.CurrentItem as IEntry;
    }

    int IEntryObservationController.CurrentPosition() {
        return ListView.Items.CurrentPosition;
    }

    bool IEntryObservationController.CurrentPosition(int value) {
        var items = ListView.Items;
        if (items.Count > value) {
            return items.MoveCurrentToPosition(value);
        }
        return false;
    }

    bool IEntryObservationController.Focus() {
        if (Log.Info()) {
            Log.Info(nameof(IEntryObservationController.Focus));
        }
        var currentPosition = ListView.Items.CurrentPosition;
        if (currentPosition < 0) {
            if (ListView.Items.Count > 0) {
                ListView.Items.MoveCurrentToFirst();
                if (Log.Info()) {
                    Log.Info(nameof(ListView.Items.MoveCurrentToFirst));
                }
            }
        }
        var currentItem = ListView.Items.CurrentItem;
        if (currentItem != null) {
            var listViewItem = ListView.ItemContainerGenerator.ContainerFromItem(currentItem) as ListViewItem;
            if (listViewItem != null) {
                var focus = listViewItem.IsKeyboardFocused || listViewItem.Focus();
                if (Log.Info()) {
                    Log.Info(
                        $"{nameof(listViewItem.IsKeyboardFocused)} > {listViewItem.IsKeyboardFocused}",
                        $"{nameof(focus)} > {focus}");
                }
                return focus;
            }
        }
        var entryListViewFocus = ListView.Focus();
        if (Log.Info()) {
            Log.Info($"{nameof(entryListViewFocus)} > {entryListViewFocus}");
        }
        return entryListViewFocus;
    }

    void IEntryObservationController.Sort(IEntrySorting sorting) {
        foreach (var list in GridColumns.Values) {
            foreach (var column in list) {
                if (sorting == null) {
                    column.Sorting(null);
                }
                else {
                    if (sorting.TryGetValue(column.Key, out var value)) {
                        column.Sorting(value);
                    }
                    else {
                        column.Sorting(null);
                    }
                }
            }
        }
        CustomSort = sorting == null
            ? null
            : new EntryComparer(sorting);
        var view = ListCollectionView;
        if (view != null) {
            view.CustomSort = CustomSort;
        }
    }

    bool IEntryObservationController.AddData(IEntryDataDefinition definition) {
        if (definition == null) {
            return false;
        }
        var key = definition.Key;
        var
        col = new EntryGridViewColumnProxy(definition, Element.Translation);
        col.AddTo(GridView);
        var
        list = GridColumnList(key);
        list.Add(col);
        var sort = ListCollectionView?.CustomSort as EntryComparer;
        if (sort != null) {
            var sorting = sort.Sorting;
            if (sorting != null) {
                if (sorting.TryGetValue(key, out var value)) {
                    col.Sorting(value);
                }
            }
        }
        return true;
    }

    bool IEntryObservationController.RemoveData(string key) {
        var list = GridColumnList(key);
        var removed = false;
        foreach (var column in list) {
            var
            r = column.RemoveFrom(GridView);
            removed |= r;
        }
        return removed;
    }

    bool IEntryObservationController.HasData(string key) {
        var list = GridColumnList(key);
        return list.Count > 0;
    }

    void IEntryObservationController.ClearData() {
        GridView?.Columns.Clear();
        GridColumns.Clear();
    }

    bool IEntryObservationController.Focused() {
        return ListView.IsKeyboardFocusWithin;
    }

    bool IEntryObservationController.SizeToFit(string key) {
        var grid = GridView;
        if (grid is null) {
            return false;
        }
        var result = false;
        foreach (var column in grid.Columns.OfType<EntryGridViewColumn>()) {
            if (column.DataKey != key) {
                continue;
            }
            if (column.Owner is not null) {
                column.Owner.RequestFit(column);
            }
            else {
                column.Width = column.ActualWidth;
                column.Width = double.NaN;
            }
            result = true;
        }
        return result;
    }
}
