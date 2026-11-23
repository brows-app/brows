using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryColumnSizingTest {
    private sealed class Clock : TimeProvider {
        internal long Now;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() {
            return Now;
        }
    }

    internal sealed class Data : IEntryData, INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        public object Value { get; private set; }
        public string Display => Value?.ToString();
        public string Key => "Test";
        public EntryDataAlignment Alignment => EntryDataAlignment.Right;
        internal int ReadyReads { get; private set; }
        public bool Ready {
            get {
                ReadyReads++;
                return true;
            }
        }
        Task IEntryData.Ready => Task.CompletedTask;
        public Task Refresh(CancellationToken token) {
            return Task.CompletedTask;
        }
        public int Compare(IEntryData other) {
            return 0;
        }
        internal void Set(string value) {
            Value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Display)));
        }
    }

    internal sealed class Probe : Decorator {
        internal int NaturalCalls { get; private set; }
        protected override Size MeasureOverride(Size constraint) {
            if (double.IsPositiveInfinity(constraint.Width)) {
                NaturalCalls++;
            }
            return base.MeasureOverride(constraint);
        }
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text", typeof(string), typeof(Probe), new PropertyMetadata(null, (sender, args) => {
                ((TextBlock)((Probe)sender).Child).Text = (string)args.NewValue;
            }));
        public Probe() {
            Child = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        }
    }

    [Test]
    public void Sampling_ReusesNormalLayoutAndDoesNotResizeForConstantWidthUpdates() {
        DispatcherTools.Run(dispatcher => {
            var clock = new Clock();
            var data = new Data();
            data.Set("1234");
            var entry = new Mock<IEntry>();
            entry.Setup(e => e["Value"]).Returns(data);
            var list = new EntryListViewItemsListView(clock);
            var view = new EntryGridView();
            var column = Column("Value");
            var content = new FrameworkElementFactory(typeof(Probe));
            content.SetBinding(Probe.TextProperty, new Binding("Item[Value].Display"));
            column.CellTemplate = EntryGridViewColumnCellTemplate.Wrap(
                column, new DataTemplate { VisualTree = content });
            view.Columns.Add(column);
            list.View = view;
            list.ItemsSource = new[] { entry.Object };
            var window = EntryGridViewportTest.CreateWindow(list);
            try {
                window.Show();
                DispatcherTools.Pump(dispatcher);
                var probe = EntryGridViewportTest.Descendants<Probe>(list).Single();
                var initial = probe.NaturalCalls;
                var writes = 0;
                ((INotifyPropertyChanged)column).PropertyChanged += (_, e) => {
                    if (e.PropertyName == "Width") {
                        writes++;
                    }
                };
                for (var i = 1000; i < 2000; i++) {
                    data.Set(i.ToString());
                }
                DispatcherTools.Pump(dispatcher);
                window.UpdateLayout();
                Assert.That(probe.NaturalCalls, Is.EqualTo(initial));
                clock.Now += 100;
                list.Sizing.Update();
                window.UpdateLayout();
                Assert.That(probe.NaturalCalls, Is.EqualTo(initial + 1));
                Assert.That(writes, Is.Zero);
                var cell = EntryGridViewportTest.Descendants<EntryColumnMeasure>(list).Single(c => !c.IsHeader);
                probe.Measure(new Size(double.PositiveInfinity, 100));
                Assert.That(cell.NaturalWidth, Is.EqualTo(probe.DesiredSize.Width + 12).Within(0.001));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Virtualization_LoadsOnlyRealizedCellsAndSourceChangeRestoresAutomaticMode() {
        DispatcherTools.Run(dispatcher => {
            var data = Enumerable.Range(0, 1000).Select(_ => new Data()).ToArray();
            var entries = data.Select(value => {
                var entry = new Mock<IEntry>();
                entry.Setup(e => e["Name"]).Returns(value);
                return entry.Object;
            }).ToArray();
            var list = new EntryListViewItemsListView();
            var view = new EntryGridView();
            var column = Column("Name");
            view.Columns.Add(column);
            list.View = view;
            list.ItemsSource = entries;
            var window = EntryGridViewportTest.CreateWindow(list);
            try {
                window.Show();
                DispatcherTools.Pump(dispatcher);
                Assert.That(data.Count(value => value.ReadyReads > 0), Is.InRange(1, 100));
                Assert.That(data[^1].ReadyReads, Is.Zero);
                column.Width = 80;
                list.ItemsSource = new[] { entries[^1] };
                DispatcherTools.Pump(dispatcher);
                Assert.That(column.Mode, Is.EqualTo(EntryGridViewColumn.SizingMode.Adaptive));
                Assert.That(column.Width, Is.GreaterThan(400));
                Assert.That(data[^1].ReadyReads, Is.GreaterThan(0));
                window.Close();
                DispatcherTools.Pump(dispatcher);
                data[^1].Set(new string('W', 500));
                list.Sizing.Update();
                Assert.That(column.Owner, Is.Null);
            }
            finally {
                window.Close();
            }
        });
    }

    private static EntryGridViewColumn Column(string key, double hint = 100) {
        var column = new EntryGridViewColumn(key, hint);
        var factory = new FrameworkElementFactory(typeof(EntryDataControl));
        factory.SetBinding(EntryDataControl.ViewModelProperty, new Binding($"Item[{key}]"));
        column.CellTemplate = EntryGridViewColumnCellTemplate.Wrap(column, new DataTemplate { VisualTree = factory });
        column.Header = new EntryGridViewColumnHeader { Column = column, Display = key };
        return column;
    }

    [Test]
    public void AutoFit_DisabledIgnoresUpdatesAndResizeUntilEnabled() {
        DispatcherTools.Run(dispatcher => {
            var clock = new Clock();
            var data = new Data();
            var entry = new Mock<IEntry>();
            entry.Setup(e => e["Name"]).Returns(data);
            var list = new EntryListViewItemsListView(clock);
            list.Sizing.SetAutoFit(false);
            var view = new EntryGridView();
            var column = Column("Name");
            view.Columns.Add(column);
            list.View = view;
            list.ItemsSource = new[] { entry.Object };
            var window = EntryGridViewportTest.CreateWindow(list);
            try {
                window.Show();
                DispatcherTools.Pump(dispatcher);
                Assert.That(column.Width, Is.EqualTo(100));
                Assert.That(column.Owner, Is.Null);
                data.Set(new string('W', 100));
                window.Width = 800;
                list.Sizing.Retemplate();
                list.Sizing.Attach();
                clock.Now += 100;
                DispatcherTools.Pump(dispatcher);
                Assert.That(column.Width, Is.EqualTo(100));
                Assert.That(column.NaturalWidth, Is.Zero);
                list.Sizing.SetAutoFit(true);
                DispatcherTools.Pump(dispatcher);
                Assert.That(column.Width, Is.GreaterThan(600));
                Assert.That(column.Owner, Is.SameAs(list.Sizing));
                data.Set(new string('W', 200));
                list.Sizing.SetAutoFit(false);
                var width = column.Width;
                clock.Now += 100;
                window.Width = 400;
                DispatcherTools.Pump(dispatcher);
                list.Sizing.Update();
                Assert.That(column.Width, Is.EqualTo(width));
                Assert.That(column.Owner, Is.Null);
                list.Sizing.SetAutoFit(true);
                DispatcherTools.Pump(dispatcher);
                Assert.That(column.Width, Is.LessThan(400));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void AutoFit_ControllerKeepsExplicitFitAvailableWhenDisabled() {
        DispatcherTools.Run(dispatcher => {
            var entryList = new EntryListView();
            var window = EntryGridViewportTest.CreateWindow(entryList);
            try {
                window.Show();
                DispatcherTools.Pump(dispatcher);
                var list = entryList.ItemsListView;
                var controller = new EntryObservationController(new EntryObservationControl());
                controller.ListViewChanged(null, entryList);
                var contract = (IEntryObservationController)controller;
                contract.AutoFit = false;
                var data = new Data();
                data.Set(new string('W', 100));
                var entry = new Mock<IEntry>();
                entry.Setup(e => e["Name"]).Returns(data);
                var column = Column("Name");
                entryList.GridView.Columns.Add(column);
                list.ItemsSource = new[] { entry.Object };
                DispatcherTools.Pump(dispatcher);
                Assert.That(list.Sizing.AutoFit, Is.False);
                Assert.That(column.Width, Is.EqualTo(100));
                Assert.That(contract.SizeToFit("Name"), Is.True);
                window.UpdateLayout();
                DispatcherTools.Pump(dispatcher);
                Assert.That(column.Width, Is.NaN);
                Assert.That(column.ActualWidth, Is.GreaterThan(400));
                Assert.That(contract.SizeToFit("Missing"), Is.False);
                Assert.That(list.Sizing.AutoFit, Is.False);
                contract.AutoFit = true;
                DispatcherTools.Pump(dispatcher);
                Assert.That(column.Width, Is.Not.NaN);
                Assert.That(column.Width, Is.LessThan(window.ActualWidth));
                Assert.That(column.Owner, Is.SameAs(list.Sizing));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Updates_AreSampledAtBoundedCadenceAndFitOverridesBudget() {
        DispatcherTools.Run(dispatcher => {
            var clock = new Clock();
            var data = new Data();
            var entry = new Mock<IEntry>();
            entry.Setup(e => e[It.IsAny<string>()]).Returns(data);
            var list = new EntryListViewItemsListView(clock);
            var view = new EntryGridView();
            var name = Column("Name", 250);
            var value = Column("Value");
            view.Columns.Add(name);
            view.Columns.Add(value);
            list.View = view;
            list.ItemsSource = new[] { entry.Object };
            var window = EntryGridViewportTest.CreateWindow(list);
            try {
                window.Show();
                DispatcherTools.Pump(dispatcher);
                var cells = EntryGridViewportTest.Descendants<EntryColumnMeasure>(list)
                    .Where(cell => !cell.IsHeader).ToArray();
                Assert.That(cells, Has.Length.EqualTo(2));
                var texts = EntryGridViewportTest.Descendants<EntryDataTextBlock>(list).ToArray();
                Assert.That(texts, Has.Length.EqualTo(2));
                Assert.That(texts.Select(text => text.TextAlignment), Is.All.EqualTo(TextAlignment.Right));
                Assert.That(texts.Select(text => text.TextTrimming), Is.All.EqualTo(TextTrimming.CharacterEllipsis));
                Assert.That(name.Owner, Is.SameAs(list.Sizing));
                var previous = value.NaturalWidth;
                data.Set(new string('W', 100));
                DispatcherTools.Pump(dispatcher);
                Assert.That(value.NaturalWidth, Is.EqualTo(previous));
                clock.Now += 100;
                list.Sizing.Update();
                window.UpdateLayout();
                Assert.That(value.NaturalWidth, Is.GreaterThan(500));
                Assert.That(value.Width, Is.LessThanOrEqualTo(201));
                Assert.That(name.Width + value.Width, Is.GreaterThan(400));
                list.Sizing.RequestFit(value);
                DispatcherTools.Pump(dispatcher);
                window.UpdateLayout();
                Assert.That(value.Width, Is.GreaterThan(500));
                Assert.That(name.Width, Is.GreaterThanOrEqualTo(120));
                var outer = EntryGridViewportTest.Descendants<ScrollViewer>(list).First();
                Assert.That(outer.ScrollableWidth, Is.GreaterThan(50));
                value.Width = 150;
                data.Set("Short");
                clock.Now += 100;
                list.Sizing.Update();
                Assert.That(value.Width, Is.EqualTo(150));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Resize_RestoresHiddenColumnsAndDetachStopsOldViewUpdates() {
        DispatcherTools.Run(dispatcher => {
            var list = new EntryListViewItemsListView();
            var view = new EntryGridView();
            foreach (var key in new[] { "Name", "Size", "Date", "Extension" }) {
                view.Columns.Add(Column(key));
            }
            list.View = view;
            var window = EntryGridViewportTest.CreateWindow(list);
            try {
                window.Width = 180;
                window.Show();
                DispatcherTools.Pump(dispatcher);
                Assert.That(view.Columns[0].Width, Is.GreaterThan(0));
                Assert.That(view.Columns[^1].Width, Is.Zero);
                window.Width = 800;
                window.UpdateLayout();
                DispatcherTools.Pump(dispatcher);
                Assert.That(view.Columns.Cast<GridViewColumn>().All(column => column.Width > 0), Is.True);
                var widths = view.Columns.Select(column => column.Width).ToArray();
                list.View = new EntryGridView();
                DispatcherTools.Pump(dispatcher);
                window.Width = 300;
                window.UpdateLayout();
                DispatcherTools.Pump(dispatcher);
                Assert.That(view.Columns.Select(column => column.Width), Is.EqualTo(widths));
                Assert.That(view.Columns.Cast<EntryGridViewColumn>().All(column => column.Owner is null), Is.True);
            }
            finally {
                window.Close();
            }
        });
    }
}
