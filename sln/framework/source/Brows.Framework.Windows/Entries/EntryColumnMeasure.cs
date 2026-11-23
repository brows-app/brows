using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brows.Entries;

internal sealed class EntryColumnMeasure : Decorator {
    private EntryColumnSizing Owner;
    private INotifyPropertyChanged Notifications;
    private Size Constraint;

    private void DataChanged(object sender, PropertyChangedEventArgs e) {
        var affectsWidth = string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName is "Value" or "Display" or "Ready";
        if (affectsWidth) {
            Owner?.Dirty(this);
        }
    }

    private void Subscribe() {
        if (Notifications is not null) {
            Notifications.PropertyChanged -= DataChanged;
        }
        Notifications = IsLoaded ? Data as INotifyPropertyChanged : null;
        if (Notifications is not null) {
            Notifications.PropertyChanged += DataChanged;
        }
        Owner?.Dirty(this);
    }

    private void LoadedMeasure(object sender, RoutedEventArgs e) {
        Owner = EntryColumnSizing.Ancestor<EntryListViewItemsListView>(this)?.Sizing;
        Owner?.Register(this);
        Subscribe();
    }

    private void UnloadedMeasure(object sender, RoutedEventArgs e) {
        if (Notifications is not null) {
            Notifications.PropertyChanged -= DataChanged;
            Notifications = null;
        }
        Owner?.Unregister(this);
        Owner = null;
    }

    internal double NaturalWidth { get; private set; }

    internal void Sample() {
        if (Child is null) {
            return;
        }
        try {
            Child.Measure(new Size(double.PositiveInfinity, Constraint.Height));
            var chrome = 0.0;
            if (IsHeader) {
                var header = EntryColumnSizing.Ancestor<GridViewColumnHeader>(this);
                if (header is not null) {
                    chrome = header.Padding.Left + header.Padding.Right +
                        header.BorderThickness.Left + header.BorderThickness.Right;
                }
            }
            else {
                var presenter = VisualTreeHelper.GetParent(this) as ContentPresenter;
                if (presenter is not null) {
                    chrome = presenter.Margin.Left + presenter.Margin.Right;
                }
            }
            NaturalWidth = Child.DesiredSize.Width + chrome;
        }
        finally {
            if (Constraint.Width > 0) {
                Child.Measure(Constraint);
            }
        }
    }

    protected override Size MeasureOverride(Size constraint) {
        var changed = constraint != Constraint;
        Constraint = constraint;
        if (changed) {
            Owner?.Dirty(this);
        }
        if (constraint.Width == 0) {
            return new Size();
        }
        return base.MeasureOverride(constraint);
    }

    protected override void OnChildDesiredSizeChanged(UIElement child) {
        base.OnChildDesiredSizeChanged(child);
        Owner?.Dirty(this);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        var typography = e.Property == TextBlock.FontSizeProperty || e.Property == TextBlock.FontFamilyProperty ||
            e.Property == TextBlock.FontWeightProperty || e.Property == TextBlock.FontStyleProperty ||
            e.Property == TextBlock.FontStretchProperty || e.Property == TextOptions.TextFormattingModeProperty;
        if (typography) {
            Owner?.ResetMeasurements();
        }
    }

    public static readonly DependencyProperty ColumnProperty = DependencyProperty.Register(
        nameof(Column), typeof(EntryGridViewColumn), typeof(EntryColumnMeasure));
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(IEntryData), typeof(EntryColumnMeasure),
        new PropertyMetadata(null, (sender, _) => ((EntryColumnMeasure)sender).Subscribe()));
    public static readonly DependencyProperty IsHeaderProperty = DependencyProperty.Register(
        nameof(IsHeader), typeof(bool), typeof(EntryColumnMeasure));

    public EntryGridViewColumn Column {
        get => (EntryGridViewColumn)GetValue(ColumnProperty);
        set => SetValue(ColumnProperty, value);
    }

    public IEntryData Data {
        get => (IEntryData)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public bool IsHeader {
        get => (bool)GetValue(IsHeaderProperty);
        set => SetValue(IsHeaderProperty, value);
    }

    public EntryColumnMeasure() {
        ClipToBounds = true;
        Loaded += LoadedMeasure;
        Unloaded += UnloadedMeasure;
    }
}
