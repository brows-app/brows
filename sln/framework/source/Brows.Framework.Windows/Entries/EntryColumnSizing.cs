using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Brows.Entries;

internal sealed class EntryColumnSizing {
    private static readonly ILog Log = Logging.For(typeof(EntryColumnSizing));
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);
    private readonly Lock Locker = new();
    private readonly HashSet<EntryColumnMeasure> Cells = [];
    private readonly HashSet<EntryColumnMeasure> Pending = [];
    private readonly HashSet<EntryGridViewColumn> Columns = [];
    private readonly EntryListViewItemsListView List;
    private readonly TimeProvider Clock;
    private readonly DispatcherTimer Timer;
    private GridView Grid;
    private ScrollContentPresenter Body;
    private ScrollContentPresenter HeaderViewport;
    private GridViewHeaderRowPresenter Header;
    private bool Queued;
    private bool LayoutPending;
    private bool Sampling;
    private bool ResetPending;
    private bool RestoreModes;
    private bool Prompt;
    private long? LastSample;
    private double BudgetCeiling = double.PositiveInfinity;

    private void Request(bool layout = false) {
        lock (Locker) {
            if (!AutoFit) {
                return;
            }
            LayoutPending |= layout;
            if (Queued) {
                return;
            }
            Queued = true;
        }
        _ = List.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Update));
    }

    private void ColumnsChanged(object sender, NotifyCollectionChangedEventArgs e) {
        foreach (var column in Columns) {
            column.Owner = null;
        }
        Columns.Clear();
        if (Grid is not null) {
            foreach (var column in Grid.Columns.OfType<EntryGridViewColumn>()) {
                Columns.Add(column);
                column.Owner = this;
            }
        }
        Prompt = true;
        BudgetCeiling = double.PositiveInfinity;
        Request(layout: true);
    }

    private void ViewportChanged(object sender, SizeChangedEventArgs e) {
        Request(layout: true);
    }

    private bool FindViewport() {
        if (Body is not null) {
            return true;
        }
        var outer = Find<ScrollViewer>(List);
        if (outer is null) {
            return false;
        }
        Body = outer.Template?.FindName("PART_ScrollContentPresenter", outer) as ScrollContentPresenter;
        Header = Find<GridViewHeaderRowPresenter>(outer);
        var headerViewer = Ancestor<ScrollViewer>(Header);
        HeaderViewport = headerViewer?.Template?.FindName(
            "PART_ScrollContentPresenter", headerViewer) as ScrollContentPresenter;
        var found = Body is not null && HeaderViewport is not null;
        if (!found) {
            Body = null;
            return false;
        }
        Body.SizeChanged += ViewportChanged;
        HeaderViewport.SizeChanged += ViewportChanged;
        return true;
    }

    private void ClearViewport() {
        if (Body is not null) {
            Body.SizeChanged -= ViewportChanged;
        }
        if (HeaderViewport is not null) {
            HeaderViewport.SizeChanged -= ViewportChanged;
        }
        Body = HeaderViewport = null;
        Header = null;
    }

    private void Tick(object sender, EventArgs e) {
        Update();
    }

    private static IEnumerable<EntryColumnMeasure> Descendants(DependencyObject root) {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is EntryColumnMeasure cell) {
                yield return cell;
            }
            foreach (var item in Descendants(child)) {
                yield return item;
            }
        }
    }

    internal bool AutoFit { get; private set; } = true;

    internal EntryColumnSizing(EntryListViewItemsListView list, TimeProvider clock = null) {
        List = list ?? throw new ArgumentNullException(nameof(list));
        Clock = clock ?? TimeProvider.System;
        Timer = new DispatcherTimer(DispatcherPriority.Background, list.Dispatcher) { Interval = Interval };
        Timer.Tick += Tick;
    }

    internal void SetAutoFit(bool value) {
        if (AutoFit == value) {
            return;
        }
        AutoFit = value;
        if (!value) {
            Detach();
            return;
        }
        Attach();
        ResetMeasurements(restoreModes: true);
    }

    internal static T Ancestor<T>(DependencyObject child) where T : DependencyObject {
        var parent = child is null ? null : VisualTreeHelper.GetParent(child);
        while (parent is not null) {
            if (parent is T value) {
                return value;
            }
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    internal static T Find<T>(DependencyObject parent) where T : DependencyObject {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T value) {
                return value;
            }
            var found = Find<T>(child);
            if (found is not null) {
                return found;
            }
        }
        return null;
    }

    internal void Attach() {
        Detach();
        if (!AutoFit) {
            return;
        }
        Grid = List.View as EntryGridView;
        if (Grid is null) {
            return;
        }
        ((INotifyCollectionChanged)Grid.Columns).CollectionChanged += ColumnsChanged;
        ColumnsChanged(this, null);
        foreach (var cell in Descendants(List)) {
            Register(cell);
        }
    }

    internal void Detach() {
        Timer.Stop();
        ClearViewport();
        if (Grid is not null) {
            ((INotifyCollectionChanged)Grid.Columns).CollectionChanged -= ColumnsChanged;
        }
        foreach (var column in Columns) {
            column.Owner = null;
        }
        Columns.Clear();
        Grid = null;
        lock (Locker) {
            Pending.Clear();
            Cells.Clear();
        }
        LastSample = null;
        BudgetCeiling = double.PositiveInfinity;
    }

    internal void Register(EntryColumnMeasure cell) {
        lock (Locker) {
            if (!AutoFit) {
                return;
            }
            Cells.Add(cell);
            Pending.Add(cell);
        }
        Prompt = true;
        Request(layout: true);
    }

    internal void Unregister(EntryColumnMeasure cell) {
        lock (Locker) {
            Cells.Remove(cell);
            Pending.Remove(cell);
        }
    }

    internal void Dirty(EntryColumnMeasure cell) {
        lock (Locker) {
            var ignores = Sampling && List.Dispatcher.CheckAccess();
            if (ignores) {
                return;
            }
            if (!Cells.Contains(cell)) {
                return;
            }
            Pending.Add(cell);
        }
        Request();
    }

    internal void ResetMeasurements(bool restoreModes = false) {
        RestoreModes |= restoreModes;
        ResetPending = true;
        Prompt = true;
        BudgetCeiling = double.PositiveInfinity;
        Request();
    }

    internal void ResetBudget() {
        BudgetCeiling = double.PositiveInfinity;
        Request();
    }

    internal void DirtyHeaders(EntryGridViewColumn column) {
        lock (Locker) {
            Pending.UnionWith(Cells.Where(cell => cell.Column == column && cell.IsHeader));
        }
        Request();
    }

    internal void Retemplate() {
        ClearViewport();
        ResetMeasurements();
    }

    internal void RequestFit(EntryGridViewColumn column, bool content = true) {
        if (content) {
            column.Mode = EntryGridViewColumn.SizingMode.Content;
            column.ContentWidth = 0;
            lock (Locker) {
                foreach (var cell in Cells.Where(cell => cell.Column == column)) {
                    Pending.Add(cell);
                }
            }
            Prompt = true;
        }
        Request(layout: true);
    }

    internal void Update() {
        bool layout;
        lock (Locker) {
            layout = LayoutPending;
            LayoutPending = false;
            Queued = false;
        }
        var ready = AutoFit && List.IsLoaded && Grid is not null && FindViewport();
        if (!ready) {
            Timer.Stop();
            return;
        }
        if (ResetPending) {
            foreach (var column in Columns) {
                column.Reset(RestoreModes);
            }
            lock (Locker) {
                Pending.UnionWith(Cells);
            }
            ResetPending = false;
            RestoreModes = false;
        }
        var scale = VisualTreeHelper.GetDpi(List).DpiScaleX;
        var due = Prompt || LastSample is null || Clock.GetElapsedTime(LastSample.Value) >= Interval;
        if (due) {
            EntryColumnMeasure[] pending;
            lock (Locker) {
                pending = Pending.ToArray();
                Pending.Clear();
            }
            Sampling = true;
            try {
                foreach (var cell in pending) {
                    var column = cell.Column;
                    var samples = column is not null && Columns.Contains(column) && cell.IsLoaded &&
                        column.Mode != EntryGridViewColumn.SizingMode.Fixed;
                    if (!samples) {
                        continue;
                    }
                    try {
                        var previous = column.Mode == EntryGridViewColumn.SizingMode.Content
                            ? Math.Max(column.HeaderWidth, column.ContentWidth) : column.Preferred;
                        cell.Sample();
                        var width = cell.NaturalWidth;
                        if (!double.IsFinite(width)) {
                            continue;
                        }
                        if (cell.IsHeader) {
                            column.HeaderWidth = width;
                        }
                        else {
                            column.ContentWidth = Math.Max(column.ContentWidth, width);
                            if (cell.Data?.Value is not null) {
                                column.HasValue = true;
                                column.NaturalWidth = Math.Max(column.NaturalWidth, width);
                            }
                        }
                        var current = column.Mode == EntryGridViewColumn.SizingMode.Content
                            ? Math.Max(column.HeaderWidth, column.ContentWidth) : column.Preferred;
                        layout |= Math.Abs(current - previous) >= 1 / scale;
                    }
                    catch (Exception ex) {
                        if (Log.Warn()) {
                            Log.Warn(ex);
                        }
                    }
                }
            }
            finally {
                Sampling = false;
            }
            LastSample = Clock.GetTimestamp();
            Prompt = false;
        }
        if (layout) {
            /*
             * Brows rows have 2 DIP left padding and the row presenter reserves 2 DIP at the end.
             * List padding and scrollbar space are already outside these measured viewports.
             */
            var bodyBudget = Body.ActualWidth - 4;
            var headerBudget = HeaderViewport.ActualWidth - Header.Margin.Left - Header.Margin.Right;
            var budget = Math.Max(0, Math.Min(bodyBudget, headerBudget));
            BudgetCeiling = Math.Min(BudgetCeiling, budget);
            var columns = Grid.Columns.OfType<EntryGridViewColumn>().ToArray();
            var demands = columns.Select(column => new EntryColumnLayout.Column(column.Preferred, column.HeaderWidth,
                column.Mode == EntryGridViewColumn.SizingMode.Fixed ? column.Width :
                column.Mode == EntryGridViewColumn.SizingMode.Content
                    ? Math.Max(column.HeaderWidth, column.ContentWidth) :
                (double?)null)).ToArray();
            var widths = EntryColumnLayout.Allocate(demands, BudgetCeiling, scale);
            for (var i = 0; i < columns.Length; i++) {
                var changed = double.IsNaN(columns[i].Width) || Math.Abs(columns[i].Width - widths[i]) > 0.000001;
                if (changed) {
                    columns[i].SetWidth(widths[i]);
                }
            }
        }
        lock (Locker) {
            Pending.RemoveWhere(cell => cell.Column?.Mode == EntryGridViewColumn.SizingMode.Fixed);
            if (Pending.Count > 0) {
                Timer.Start();
            }
            else {
                Timer.Stop();
            }
        }
    }
}
