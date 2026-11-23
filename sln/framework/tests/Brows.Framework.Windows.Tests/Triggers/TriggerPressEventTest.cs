using Brows.Commands;
using Brows.Entries;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Brows.Triggers;

[TestFixture]
internal sealed class TriggerPressEventTest {
    private static bool DoubleClickSourcesEntry(EntryDataAlignment alignment, double x) {
        var source = default(object);
        var entry = default(IEntry);
        DispatcherTools.Run(dispatcher => {
            var list = new EntryListViewItemsListView();
            var window = EntryGridViewportTest.CreateWindow(list);
            try {
                var entryMock = new Mock<IEntry>();
                var entrySource = entryMock.As<ICommandSourceObject>();
                entry = entryMock.Object;
                var data = new TestEntryData(entry, alignment);
                entryMock.Setup(item => item[It.IsAny<string>()]).Returns(data);
                entrySource.SetupGet(item => item.Instance).Returns(entry);
                entrySource.SetupGet(item => item.Collection).Returns(new[] { entry });
                var definition = new Mock<IEntryDataDefinition>();
                definition.SetupGet(item => item.Key).Returns(TestEntryData.DataKey);
                definition.SetupGet(item => item.Alignment).Returns(alignment);
                var grid = new EntryGridView();
                grid.Columns.Add(new EntryGridViewColumn(TestEntryData.DataKey) {
                    CellTemplate = EntryGridViewColumnCellTemplate.Get(definition.Object),
                    Width = 300
                });
                list.View = grid;
                list.ItemContainerStyle = (Style)list.FindResource("Brows.Style.EntryListViewItem");
                list.ItemsSource = new[] { entry };
                window.Show();
                DispatcherTools.Pump(dispatcher);
                window.UpdateLayout();
                var row = EntryGridViewportTest.Descendants<GridViewRowPresenter>(list).First();
                var point = row.TranslatePoint(new Point(x, row.ActualHeight / 2), window);
                var hit = window.InputHitTest(point) as UIElement;
                Assert.That(hit, Is.Not.Null);
                var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) {
                    RoutedEvent = Control.PreviewMouseDoubleClickEvent
                };
                hit.RaiseEvent(args);
                args.Trigger(clicks: 2, gesture => source = gesture.Source);
            }
            finally {
                window.Close();
            }
        });
        Assert.That(entry, Is.Not.Null);
        return new CommandSource(source).Item is IEntry sourced && ReferenceEquals(sourced, entry);
    }

    [TestCase(EntryDataAlignment.Left, 10)]
    [TestCase(EntryDataAlignment.Left, 150)]
    [TestCase(EntryDataAlignment.Right, 150)]
    [TestCase(EntryDataAlignment.Right, 290)]
    public void Trigger_DoubleClickInEntryCell_SourcesEntry(EntryDataAlignment alignment, double x) {
        var sourcesEntry = DoubleClickSourcesEntry(alignment, x);
        Assert.That(sourcesEntry, Is.True);
    }

    private sealed class TestEntryData : IEntryData, ICommandSourceObject {
        private IEntry Entry { get; }

        public const string DataKey = "Size";

        public string Key => DataKey;
        public object Value => Display;
        public bool Ready => true;
        public string Display => "12 KB";
        public EntryDataAlignment Alignment { get; }

        public TestEntryData(IEntry entry, EntryDataAlignment alignment) {
            Entry = entry;
            Alignment = alignment;
        }

        public Task Refresh(CancellationToken token) {
            return Task.CompletedTask;
        }

        public int Compare(IEntryData other) {
            return 0;
        }

        Task IEntryData.Ready => Task.CompletedTask;

        object ICommandSourceObject.Instance => Entry;

        IEnumerable ICommandSourceObject.Collection => new[] { Entry };
    }
}
