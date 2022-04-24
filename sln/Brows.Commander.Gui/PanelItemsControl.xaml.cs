using System.Collections.Specialized;
using System.Windows;

namespace Brows {
    partial class PanelItemsControl {
        private PanelGrid PanelGrid =>
            _PanelGrid ?? (
            _PanelGrid = (PanelGrid)Template.FindName("PanelGrid", this));
        private PanelGrid _PanelGrid;

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e) {
            if (e != null) {
                var newItems = e.NewItems;
                if (newItems != null) {
                    foreach (var item in newItems) {
                        if (item is IPanel panel) {
                            PanelGrid.AddPanel(panel);
                        }
                    }
                }
                var oldItems = e.OldItems;
                if (oldItems != null) {
                    foreach (var item in oldItems) {
                        if (item is IPanel panel) {
                            PanelGrid.RemovePanel(panel);
                        }
                    }
                }
                foreach (var columnDefinition in PanelGrid.ColumnDefinitions) {
                    columnDefinition.Width = new GridLength(1, GridUnitType.Star);
                }
            }
            base.OnItemsChanged(e);
        }

        public PanelItemsControl() {
            InitializeComponent();
        }
    }
}





//public class ResizableItemControl : ItemsControl {
//    public ObservableCollection<FrameworkElement> _gridItems = new ObservableCollection<FrameworkElement>();
//    private Grid _grid;

//    static ResizableItemControl() {
//        DefaultStyleKeyProperty.OverrideMetadata(typeof(ResizableItemControl), new FrameworkPropertyMetadata(typeof(ResizableItemControl)));
//    }

//    public override void OnApplyTemplate() {
//        base.OnApplyTemplate();

//        if (this.Template != null) {
//            _grid = this.Template.FindName("PART_Grid", this) as Grid;
//        }

//        // Add all existing items to grid
//        foreach (var item in Items) {
//            AddItemToGrid(item);
//        }
//    }

//    /// <summary>
//    /// Called when Items in ItemsCollection changing
//    /// </summary>
//    /// <param name="e"></param>
//    protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
//        base.OnItemsChanged(e);
//        switch (e.Action) {
//            case NotifyCollectionChangedAction.Add:
//                if (Items.Count > 0) {
//                    //Add Items in Grid when new Items where add
//                    var myItem = this.Items[Items.Count - 1];
//                    AddItemToGrid(myItem);
//                }
//                break;
//        }


//    }

//    /// <summary>
//    ///  Adds Items to grid plus GridSplitter
//    /// </summary>
//    /// <param name="myItem"></param>
//    private void AddItemToGrid(object myItem) {
//        var visualItem = this.ItemTemplate.LoadContent() as FrameworkElement;
//        if (visualItem != null) {
//            visualItem.DataContext = myItem;

//            if (_grid != null) {
//                if (_grid.ColumnDefinitions.Count != 0) {
//                    _grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
//                    _grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
//                    var gridSplitter = CreateSplitter();
//                    Grid.SetColumn(gridSplitter, _grid.ColumnDefinitions.Count - 2);
//                    Grid.SetColumn(visualItem, _grid.ColumnDefinitions.Count - 1);
//                    _grid.Children.Add(gridSplitter);
//                    _grid.Children.Add(visualItem);

//                    //_grid.Children.Add(myTest);
//                }
//                else {
//                    _grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
//                    Grid.SetColumn(visualItem, _grid.ColumnDefinitions.Count - 1);
//                    _grid.Children.Add(visualItem);
//                }
//            }
//        }

//    }

//    private static GridSplitter CreateSplitter() {
//        var gridSplitter = new GridSplitter() { ResizeDirection = GridResizeDirection.Columns };
//        gridSplitter.Width = 5;
//        gridSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
//        gridSplitter.Background = new SolidColorBrush(Colors.Black);
//        return gridSplitter;
//    }
//}