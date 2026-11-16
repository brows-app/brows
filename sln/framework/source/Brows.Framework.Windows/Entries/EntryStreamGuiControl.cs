using Brows.Windows.Controls;
using System.ComponentModel;
using System.Windows;

namespace Brows.Entries;

public abstract class EntryStreamGuiControl : ModelView<IEntryStreamGui> {
    static EntryStreamGuiControl() {
        FocusableProperty.OverrideMetadata(typeof(EntryStreamGuiControl), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(EntryStreamGuiControl), new FrameworkPropertyMetadata(false));
    }

    private IEntryStreamGui SubscribedViewModel;

    private void This_Loaded(object sender, RoutedEventArgs e) {
        Subscribe(ViewModel);
        EntryStreamView = ViewModel?.View;
    }

    private void This_Unloaded(object sender, RoutedEventArgs e) {
        Subscribe(null);
        EntryStreamView = null;
    }

    private void EntryStreamGui_PropertyChanged(object sender, PropertyChangedEventArgs e) {
        if (sender == ViewModel && e is not null && e.PropertyName == nameof(EntryStreamGui.View)) {
            EntryStreamView = ViewModel?.View;
        }
    }

    private static void OnEntryStreamViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is EntryStreamGuiControl control) {
            control.OnEntryStreamViewChanged(e);
        }
    }

    private void Subscribe(IEntryStreamGui newValue) {
        var oldValue = SubscribedViewModel;
        if (ReferenceEquals(oldValue, newValue)) {
            return;
        }
        if (oldValue is not null) {
            PropertyChangedEventManager.RemoveHandler(
                oldValue,
                EntryStreamGui_PropertyChanged,
                nameof(EntryStreamGui.View));
        }
        SubscribedViewModel = newValue;
        if (newValue is not null) {
            PropertyChangedEventManager.AddHandler(
                newValue,
                EntryStreamGui_PropertyChanged,
                nameof(EntryStreamGui.View));
        }
    }

    protected abstract string EntryStreamViewName { get; }

    protected EntryStreamGuiControl() {
        Loaded += This_Loaded;
        Unloaded += This_Unloaded;
        Visibility = Visibility.Hidden;
    }

    protected virtual void OnEntryStreamViewChanged(DependencyPropertyChangedEventArgs e) {
        var view = e.NewValue as string;
        Visibility = view == EntryStreamViewName
            ? Visibility.Visible
            : Visibility.Hidden;
    }

    protected override void OnViewModelChanged(IEntryStreamGui oldValue, IEntryStreamGui newValue) {
        if (IsLoaded) {
            Subscribe(newValue);
        }
        EntryStreamView = newValue?.View;
        base.OnViewModelChanged(oldValue, newValue);
    }

    public static readonly DependencyProperty EntryStreamViewProperty = DependencyProperty.Register(
        name: nameof(EntryStreamView),
        propertyType: typeof(string),
        ownerType: typeof(EntryStreamGuiControl),
        typeMetadata: new PropertyMetadata(
            defaultValue: null,
            propertyChangedCallback: OnEntryStreamViewChanged));

    public string EntryStreamView {
        get => GetValue(EntryStreamViewProperty) as string;
        set => SetValue(EntryStreamViewProperty, value);
    }
}