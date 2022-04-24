using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Brows {
    partial class PanelGrid {
        public PanelGrid() {
            InitializeComponent();
        }

        public void AddPanel(IPanel panel) {
            if (null == panel) throw new ArgumentNullException(nameof(panel));

            var control = new PanelControl();
            control.DataContext = panel;
            control.SetBinding(ColumnProperty, nameof(panel.Column));

            ColumnDefinitions.Add(new ColumnDefinition());
            Children.Add(control);

            var splitter = ColumnDefinitions.Count > 1 ? new PanelGridSplitter() : null;
            if (splitter != null) {
                splitter.DataContext = panel;
                splitter.SetBinding(ColumnProperty, nameof(panel.Column));
                control.Padding = new Thickness(splitter.Width, 0, 0, 0);
                Children.Add(splitter);
            }
        }

        public void RemovePanel(IPanel panel) {
            if (null == panel) throw new ArgumentNullException(nameof(panel));

            var children = Children;
            for (var i = 0; i < children.Count; i++) {
                var child = children[i] as FrameworkElement;
                if (child != null) {
                    var orphaned = child.DataContext == panel;
                    if (orphaned) {
                        children.RemoveAt(i--);
                        BindingOperations.ClearBinding(child, ColumnProperty);
                        child.DataContext = null;
                    }
                }
            }
            var columnDefinitions = ColumnDefinitions;
            var columnDefinitionsCount = columnDefinitions.Count;
            if (columnDefinitionsCount > 0) {
                columnDefinitions.RemoveAt(columnDefinitionsCount - 1);
            }
        }
    }
}
