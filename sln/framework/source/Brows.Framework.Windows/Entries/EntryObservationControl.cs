using Brows.Gui;
using Brows.Windows.Controls;
using System.Windows;
using System.Windows.Controls;

namespace Brows.Entries;

public sealed class EntryObservationControl : ModelView<IEntryObservation> {
    private EntryListView _ListView;
    private bool ControllerAttached;

    private EntryObservationController ObservationController =>
        field ??= new EntryObservationController(this);

    private void This_Loaded(object sender, RoutedEventArgs e) {
        UpdateController();
    }

    private void This_Unloaded(object sender, RoutedEventArgs e) {
        if (ViewModel is IControlled<IEntryObservationController> controlled) {
            controlled.Controller = null;
        }
        ControllerAttached = false;
    }

    internal EntryListView ListView => _ListView;

    internal void UpdateController(bool refresh = false) {
        if (ViewModel is not IControlled<IEntryObservationController> controlled) {
            return;
        }
        var listIsReady = _ListView?.ItemsListView is { View: GridView, ItemsSource: not null };
        var shouldAttach = IsLoaded && listIsReady;
        if (!shouldAttach) {
            controlled.Controller = null;
            ControllerAttached = false;
            return;
        }
        if (refresh && ControllerAttached) {
            ViewModel.DataView.Refresh();
            return;
        }
        if (!ControllerAttached) {
            controlled.Controller = ObservationController;
            ControllerAttached = true;
        }
    }

    protected override void OnViewModelChanged(IEntryObservation oldValue, IEntryObservation newValue) {
        if (oldValue is IControlled<IEntryObservationController> oldControlled) {
            oldControlled.Controller = null;
        }
        ControllerAttached = false;
        base.OnViewModelChanged(oldValue, newValue);
        UpdateController();
    }

    public EntryObservationControl() {
        Loaded += This_Loaded;
        Unloaded += This_Unloaded;
    }

    public override void OnApplyTemplate() {
        var oldListView = _ListView;
        base.OnApplyTemplate();
        var newListView = GetTemplateChild("PART_ListView") as EntryListView;
        var listViewChanged = !ReferenceEquals(oldListView, newListView);
        if (!listViewChanged) {
            return;
        }
        _ListView = newListView;
        ObservationController.ListViewChanged(oldListView, newListView);
        UpdateController();
    }
}
