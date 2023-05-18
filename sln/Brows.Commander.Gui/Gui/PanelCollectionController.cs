using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Brows.Gui {
    internal sealed class PanelCollectionController : Controller<IPanelCollectionController>, IPanelCollectionController {
        public new PanelCollectionControl Element { get; }

        public PanelCollectionController(PanelCollectionControl element) : base(element) {
            Element = element ?? throw new ArgumentNullException(nameof(element));
        }

        void IPanelCollectionController.AddPanel(IPanel panel) {
            if (null == panel) throw new ArgumentNullException(nameof(panel));

            var grid = Element.Grid;
            var control = new PanelControl();
            control.DataContext = panel;
            control.SetBinding(Grid.ColumnProperty, nameof(panel.Column));

            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(control);

            var splitter = grid.ColumnDefinitions.Count > 1 ? new PanelGridSplitter() : null;
            if (splitter != null) {
                splitter.SetValue(Grid.ColumnProperty, panel.Column);

                //splitter.DataContext = panel;
                //splitter.SetBinding(Grid.ColumnProperty, nameof(panel.Column));
                control.Padding = new Thickness(splitter.Width, 0, 0, 0);
                grid.Children.Add(splitter);
            }
            foreach (var columnDefinition in grid.ColumnDefinitions) {
                columnDefinition.Width = new GridLength(1, GridUnitType.Star);
            }
        }

        void IPanelCollectionController.RemovePanel(IPanel panel) {
            if (null == panel) throw new ArgumentNullException(nameof(panel));
            var grid = Element.Grid;
            var removed = false;
            var children = grid.Children;
            for (var i = 0; i < children.Count; i++) {
                var child = children[i] as FrameworkElement;
                var control = child as PanelControl;
                if (control != null) {
                    var orphaned = control.DataContext == panel;
                    if (orphaned) {
                        BindingOperations.ClearBinding(child, Grid.ColumnProperty);
                        children.RemoveAt(i--);
                        child.DataContext = null;
                        removed = true;
                    }
                }

                //var splitter = child as PanelGridSplitter;
                //if (splitter != null) {
                //    var col = splitter.GetValue(Grid.ColumnProperty);
                //    var column = Convert.ToInt32(col);


                //}


                //if (child != null) {
                //    var c = child.GetValue(Grid.ColumnProperty);
                //    var orphaned = col.Equals(c);
                //    if (orphaned) {
                //        children.RemoveAt(i--);
                //        BindingOperations.ClearBinding(child, Grid.ColumnProperty);
                //        child.DataContext = null;
                //    }
                //    else {
                //        if (c != null && c.Equals(0)) {
                //            var splitter = child as PanelGridSplitter;
                //            if (splitter != null) {
                //                splitter.DataContext = null;
                //                BindingOperations.ClearBinding(splitter, Grid.ColumnProperty);
                //                children.RemoveAt(i--);
                //            }
                //            var control = child as PanelControl;
                //            if (control != null) {
                //                control.Padding = new Thickness(0);
                //            }
                //        }
                //    }
                //}
            }
            if (removed) {
                var firstPanel = children.OfType<PanelControl>().FirstOrDefault(control => (control.DataContext as IPanel)?.Column == 0);
                if (firstPanel != null) {
                    firstPanel.Padding = new Thickness(0);
                }
                var orphanedSplitter = children.OfType<PanelGridSplitter>().OrderBy(splitter => Convert.ToInt32(splitter.GetValue(Grid.ColumnProperty))).LastOrDefault();
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
    }
}
