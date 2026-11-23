using Brows.Gui;
using Brows.Panels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Brows;

[TestFixture]
[NonParallelizable]
internal sealed class PanelCollectionControlTest {
    private sealed class PanelModel {
        private readonly Mock<IPanel> _Mock = new();

        internal int Column {
            get;
            set {
                field = value;
                _Mock.As<INotifyPropertyChanged>().Raise(
                    notifier => notifier.PropertyChanged += null,
                    new PropertyChangedEventArgs(nameof(IPanel.Column)));
            }
        }

        internal IPanel Value { get; }

        internal PanelModel(int column) {
            _Mock.As<INotifyPropertyChanged>();
            _Mock.SetupGet(panel => panel.Column).Returns(() => Column);
            Value = _Mock.Object;
            Column = column;
        }
    }

    private sealed class PanelCollectionModel : IPanelCollection, IControlled<IPanelCollectionController> {
        private readonly List<PanelModel> _Panels = [];
        private IPanelCollectionController _Controller;

        internal IPanelCollectionController CurrentController => _Controller;

        internal void Add(params PanelModel[] panels) {
            foreach (var panel in panels) {
                _Panels.Add(panel);
                _Controller?.AddPanel(panel.Value);
            }
        }

        internal void Remove(PanelModel panel) {
            var removed = _Panels.Remove(panel);
            if (removed == false) {
                return;
            }
            for (var i = 0; i < _Panels.Count; i++) {
                _Panels[i].Column = i;
            }
            _Controller?.RemovePanel(panel.Value);
        }

        internal void Shift(PanelModel panel, int column) {
            var removed = _Panels.Remove(panel);
            if (removed == false) {
                return;
            }
            _Panels.Insert(column, panel);
            for (var i = 0; i < _Panels.Count; i++) {
                _Panels[i].Column = i;
            }
        }

        IPanel IPanelCollection.this[int index] => _Panels[index].Value;
        int IPanelCollection.Count => _Panels.Count;
        IPanel IPanelCollection.Active => _Panels.FirstOrDefault()?.Value;
        IPanel IPanelCollection.Passive => null;
        IReadOnlyCollection<string> IPanelCollection.History => Array.Empty<string>();

        bool IPanelCollection.HasColumn(int column, out IPanel panel) {
            var match = _Panels.FirstOrDefault(item => item.Column == column);
            panel = match?.Value;
            return match is not null;
        }

        IEnumerator<IPanel> IPanelCollection.GetEnumerator() {
            return AsEnumerable().GetEnumerator();
        }

        IEnumerable<IPanel> IPanelCollection.AsEnumerable() {
            return AsEnumerable();
        }

        IPanelCollectionController IControlled<IPanelCollectionController>.Controller {
            set {
                var oldValue = _Controller;
                if (oldValue == value) {
                    return;
                }
                oldValue?.RemovePanels(AsEnumerable());
                _Controller = value;
                _Controller?.AddPanels(AsEnumerable());
            }
        }

        private IEnumerable<IPanel> AsEnumerable() {
            return _Panels.Select(panel => panel.Value).ToList();
        }
    }

    private static ResourceDictionary Load(string name) {
        return new ResourceDictionary {
            Source = new Uri(
                $"/Brows.Commander.Windows;component/Resources/{name}.xaml",
                UriKind.Relative)
        };
    }

    private static Window CreateWindow() {
        var window = new Window {
            Height = 240,
            Left = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            Width = 600
        };
        window.Resources.MergedDictionaries.Add(new ResourceDictionary {
            Source = new Uri("/Brows.Framework.Windows;component/Resources/Styles.xaml", UriKind.Relative)
        });
        window.Resources.MergedDictionaries.Add(Load("Styles"));
        return window;
    }

    private static Grid GetPanelGrid(PanelCollectionControl control) {
        control.ApplyTemplate();
        return (Grid)control.Template.FindName("PART_Grid", control);
    }

    private static bool IsDrawnAbove(Grid grid, UIElement element, UIElement other) {
        /*
         * A panel draws its children in ascending ZIndex order, and
         * children with equal ZIndex in the order they were added.
         */
        var elementZIndex = Grid.GetZIndex(element);
        var otherZIndex = Grid.GetZIndex(other);
        if (elementZIndex != otherZIndex) {
            return elementZIndex > otherZIndex;
        }
        return grid.Children.IndexOf(element) > grid.Children.IndexOf(other);
    }

    private static Thickness SplitterPadding(FrameworkElement element) {
        var width = (double)element.FindResource("Brows.Style.PanelSplitter.Width");
        return new Thickness(width, 0, 0, 0);
    }

    [Test]
    public void RemovePanel_RemovesVisualAndReleasesItsViewModel() {
        DispatcherTools.Run(_ => {
            var model = new PanelCollectionModel();
            var first = new PanelModel(0);
            var second = new PanelModel(1);
            model.Add(first, second);

            var control = new PanelCollectionControl {
                ViewModel = model
            };
            var window = CreateWindow();
            try {
                window.Content = control;
                window.Show();
                window.UpdateLayout();

                var grid = GetPanelGrid(control);
                var removedControl = grid.Children.OfType<PanelControl>()
                    .Single(panel => panel.ViewModel == first.Value);
                var remainingControl = grid.Children.OfType<PanelControl>()
                    .Single(panel => panel.ViewModel == second.Value);

                model.Remove(first);
                window.UpdateLayout();

                Assert.Multiple(() => {
                    Assert.That(grid.Children.OfType<PanelControl>().Count(), Is.EqualTo(1));
                    Assert.That(grid.Children.OfType<PanelSplitter>().Count(), Is.Zero);
                    Assert.That(grid.ColumnDefinitions.Count, Is.EqualTo(1));
                    Assert.That(removedControl.ViewModel, Is.Null);
                    Assert.That(remainingControl.ViewModel, Is.SameAs(second.Value));
                    Assert.That(remainingControl.Padding, Is.EqualTo(new Thickness(0)));
                });
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void RemovePanel_KeepsSplittersAbovePanelsThatShiftIntoTheirColumns() {
        DispatcherTools.Run(_ => {
            var model = new PanelCollectionModel();
            var first = new PanelModel(0);
            var second = new PanelModel(1);
            var third = new PanelModel(2);
            var fourth = new PanelModel(3);
            model.Add(first, second, third, fourth);

            var control = new PanelCollectionControl {
                ViewModel = model
            };
            var window = CreateWindow();
            try {
                window.Content = control;
                window.Show();
                window.UpdateLayout();

                model.Remove(second);
                window.UpdateLayout();

                var grid = GetPanelGrid(control);
                var panels = grid.Children.OfType<PanelControl>().ToList();
                var splitters = grid.Children.OfType<PanelSplitter>().ToList();
                Assert.That(splitters.Select(Grid.GetColumn), Is.EquivalentTo(new[] { 1, 2 }));
                Assert.Multiple(() => {
                    foreach (var splitter in splitters) {
                        var column = Grid.GetColumn(splitter);
                        var panel = panels.Single(item => Grid.GetColumn(item) == column);
                        Assert.That(IsDrawnAbove(grid, splitter, panel), Is.True, $"Splitter in column {column}");
                    }
                });
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Shift_PadsOnlyPanelsThatFollowASplitter() {
        DispatcherTools.Run(_ => {
            var model = new PanelCollectionModel();
            var first = new PanelModel(0);
            var second = new PanelModel(1);
            model.Add(first, second);

            var control = new PanelCollectionControl {
                ViewModel = model
            };
            var window = CreateWindow();
            try {
                window.Content = control;
                window.Show();
                window.UpdateLayout();

                var grid = GetPanelGrid(control);
                var firstControl = grid.Children.OfType<PanelControl>()
                    .Single(panel => panel.ViewModel == first.Value);
                var secondControl = grid.Children.OfType<PanelControl>()
                    .Single(panel => panel.ViewModel == second.Value);

                model.Shift(first, 1);
                window.UpdateLayout();

                Assert.Multiple(() => {
                    Assert.That(Grid.GetColumn(secondControl), Is.Zero);
                    Assert.That(secondControl.Padding, Is.EqualTo(new Thickness(0)));
                    Assert.That(Grid.GetColumn(firstControl), Is.EqualTo(1));
                    Assert.That(firstControl.Padding, Is.EqualTo(SplitterPadding(firstControl)));
                });
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Template_ReapplicationRecreatesCurrentPanelVisuals() {
        DispatcherTools.Run(_ => {
            var model = new PanelCollectionModel();
            var first = new PanelModel(0);
            var second = new PanelModel(1);
            model.Add(first, second);

            var control = new PanelCollectionControl {
                ViewModel = model
            };
            var window = CreateWindow();
            try {
                window.Content = control;
                window.Show();
                window.UpdateLayout();

                var oldGrid = GetPanelGrid(control);
                var oldControls = oldGrid.Children.OfType<PanelControl>().ToArray();
                control.Template = new ControlTemplate(typeof(PanelCollectionControl)) {
                    VisualTree = new FrameworkElementFactory(typeof(Grid), "PART_Grid")
                };
                control.ApplyTemplate();
                window.UpdateLayout();

                var newGrid = GetPanelGrid(control);
                Assert.Multiple(() => {
                    Assert.That(newGrid, Is.Not.SameAs(oldGrid));
                    Assert.That(newGrid.Children.OfType<PanelControl>().Count(), Is.EqualTo(2));
                    Assert.That(newGrid.Children.OfType<PanelSplitter>().Count(), Is.EqualTo(1));
                    Assert.That(oldControls.All(panel => panel.ViewModel is null), Is.True);
                    Assert.That(model.CurrentController, Is.SameAs(control));
                });
            }
            finally {
                window.Close();
            }
        });
    }
}
