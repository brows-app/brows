using System;
using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace Brows.Entries;

internal sealed class EntryListViewItemsListView : ListView {
    private bool CurrentItemFocusPending;

    private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e) {
        var containersGenerated = ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated;
        if (containersGenerated) {
            RequestCurrentItemFocus();
        }
    }

    private void RequestCurrentItemFocus() {
        /*
         * Commands are sourced from the focused element, so the list itself must
         * not keep keyboard focus while it has entries. Wait for layout, so the
         * container of the current entry exists when focus is moved to it.
         */
        if (IsKeyboardFocused == false) {
            return;
        }
        if (CurrentItemFocusPending) {
            return;
        }
        CurrentItemFocusPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => {
            CurrentItemFocusPending = false;
            FocusCurrentItem();
        }));
    }

    private void FocusCurrentItem() {
        if (IsKeyboardFocused == false) {
            return;
        }
        var items = Items;
        if (items.Count == 0) {
            return;
        }
        if (items.CurrentItem is null) {
            items.MoveCurrentToFirst();
        }
        var current = items.CurrentItem;
        if (current is null) {
            return;
        }
        var container = ItemContainerGenerator.ContainerFromItem(current) as UIElement;
        if (container is null) {
            ScrollIntoView(current); // Generating the container requests focus again.
            return;
        }
        container.Focus();
    }

    internal event EventHandler ItemsSourceChanged;

    internal EntryColumnSizing Sizing {
        get => field ??= new(this);
        private set;
    }

    internal EntryListViewItemsListView(TimeProvider clock) : this() {
        Sizing = new EntryColumnSizing(this, clock);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        var viewChanged = e.Property == ViewProperty && IsLoaded;
        if (viewChanged) {
            Sizing.Attach();
        }
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) {
        base.OnDpiChanged(oldDpi, newDpi);
        Sizing.ResetMeasurements();
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e) {
        base.OnItemsChanged(e);
        Sizing.ResetBudget();
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) {
        base.OnGotKeyboardFocus(e);
        var listIsFocused = e is not null && ReferenceEquals(e.OriginalSource, this);
        if (listIsFocused) {
            RequestCurrentItemFocus();
        }
    }

    protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue) {
        Sizing.ResetMeasurements(restoreModes: true);
        ItemsSourceChanged?.Invoke(this, EventArgs.Empty);
        base.OnItemsSourceChanged(oldValue, newValue);
    }

    protected sealed override DependencyObject GetContainerForItemOverride() {
        return new EntryListViewItemContainer {
            ParentListView = this
        };
    }

    public EntryListViewItemsListView() {
        ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
        Loaded += (_, _) => Sizing.Attach();
        Unloaded += (_, _) => Sizing.Detach();
        SizeChanged += (_, _) => Sizing.ResetBudget();
    }

    public override void OnApplyTemplate() {
        base.OnApplyTemplate();
        Sizing.Retemplate();
    }
}
