using Brows.Panels;
using Brows.Windows.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Brows;

internal sealed class PanelCollectionControl : ModelView<IPanelCollection, IPanelCollectionController>,
                                               IPanelCollectionController {
    private Grid _Grid;

    private static void ClearPanelGrid(Grid grid) {
        foreach (var control in grid.Children.OfType<PanelControl>()) {
            BindingOperations.ClearBinding(control, Grid.ColumnProperty);
            BindingOperations.ClearBinding(control, Control.PaddingProperty);
            control.ViewModel = null;
            control.DataContext = null;
        }
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
    }

    public override void OnApplyTemplate() {
        var oldGrid = _Grid;
        base.OnApplyTemplate();
        var grid = GetTemplateChild("PART_Grid") as Grid;
        var templatePartChanged = oldGrid is not null && oldGrid != grid;
        if (templatePartChanged) {
            ClearPanelGrid(oldGrid);
        }
        _Grid = grid;
        if (grid is null) {
            return;
        }
        var shouldRebuildPanelGrid = IsLoaded && ViewModel is not null;
        if (shouldRebuildPanelGrid == false) {
            return;
        }
        ClearPanelGrid(grid);
        ((IPanelCollectionController)this).AddPanels(ViewModel.AsEnumerable());
    }

    void IPanelCollectionController.AddPanel(IPanel panel) {
        if (panel is null) {
            throw new ArgumentNullException(nameof(panel));
        }
        var grid = GetTemplateChild("PART_Grid") as Grid;
        if (grid is null) {
            return;
        }
        var control = new PanelControl();
        control.ViewModel = panel;
        control.DataContext = control;
        control.SetBinding(Grid.ColumnProperty, "ViewModel.Column");
        control.SetBinding(Control.PaddingProperty, new Binding("ViewModel.Column") {
            Converter = new ColumnPadding(splitterWidth())
        });

        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(control);

        var splitter = grid.ColumnDefinitions.Count > 1 ? new PanelSplitter() : null;
        if (splitter != null) {
            splitter.SetValue(Grid.ColumnProperty, panel.Column);
            grid.Children.Add(splitter);
        }
        foreach (var columnDefinition in grid.ColumnDefinitions) {
            columnDefinition.Width = new GridLength(1, GridUnitType.Star);
        }
        double splitterWidth() {
            return (double)FindResource("Brows.Style.PanelSplitter.Width");
        }
    }

    void IPanelCollectionController.AddPanels(IEnumerable<IPanel> panels) {
        if (null == panels) throw new ArgumentNullException(nameof(panels));
        foreach (var panel in panels) {
            ((IPanelCollectionController)this).AddPanel(panel);
        }
    }

    void IPanelCollectionController.RemovePanel(IPanel panel) {
        if (null == panel) throw new ArgumentNullException(nameof(panel));
        var grid = GetTemplateChild("PART_Grid") as Grid;
        if (grid is null) {
            return;
        }
        var removed = false;
        var children = grid.Children;
        for (var i = 0; i < children.Count; i++) {
            var child = children[i] as FrameworkElement;
            var control = child as PanelControl;
            if (control != null) {
                var orphaned = control.ViewModel == panel;
                if (orphaned) {
                    BindingOperations.ClearBinding(child, Grid.ColumnProperty);
                    BindingOperations.ClearBinding(child, Control.PaddingProperty);
                    control.ViewModel = null;
                    children.RemoveAt(i--);
                    child.DataContext = null;
                    removed = true;
                }
            }
        }
        if (removed) {
            var orphanedSplitter = children.OfType<PanelSplitter>().OrderBy(splitter => Convert.ToInt32(splitter.GetValue(Grid.ColumnProperty))).LastOrDefault();
            if (orphanedSplitter != null) {
                children.Remove(orphanedSplitter);
            }
            var columnDefinitions = grid.ColumnDefinitions;
            var columnDefinitionsCount = columnDefinitions.Count;
            if (columnDefinitionsCount > 0) {
                columnDefinitions.RemoveAt(columnDefinitionsCount - 1);
            }
            foreach (var columnDefinition in grid.ColumnDefinitions) {
                columnDefinition.Width = new GridLength(1, GridUnitType.Star);
            }
        }
    }

    void IPanelCollectionController.RemovePanels(IEnumerable<IPanel> panels) {
        if (null == panels) throw new ArgumentNullException(nameof(panels));
        foreach (var panel in panels) {
            ((IPanelCollectionController)this).RemovePanel(panel);
        }
    }

    private sealed class ColumnPadding : IValueConverter {
        private readonly double SplitterWidth;

        public ColumnPadding(double splitterWidth) {
            SplitterWidth = splitterWidth;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var followsSplitter = value is int column && column > 0;
            if (followsSplitter) {
                return new Thickness(SplitterWidth, 0, 0, 0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
