using Brows.Commands;
using Brows.Providers;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryListViewTest {
    private static Window Show(EntryListView view, Dispatcher dispatcher) {
        var window = EntryGridViewportTest.CreateWindow(view);
        window.Show();
        DispatcherTools.Pump(dispatcher);
        return window;
    }

    [Test]
    public void ItemsListView_SourcesCommandsFromTheObservation() {
        DispatcherTools.Run(dispatcher => {
            using var provider = new TestProvider();
            var observation = ((IEntryProvider)provider).Observation;
            var view = new EntryListView { ViewModel = observation };
            var window = Show(view, dispatcher);
            try {
                var list = view.ItemsListView;
                Assert.That(list, Is.Not.Null);

                var source = new CommandSource(list.DataContext);

                Assert.Multiple(() => {
                    Assert.That(source.Item, Is.SameAs(observation));
                    Assert.That(list.ItemsSource, Is.Not.Null);
                });
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void ColumnHeader_BindsTheSortCommandOfTheObservation() {
        DispatcherTools.Run(dispatcher => {
            using var provider = new TestProvider();
            var observation = ((IEntryProvider)provider).Observation;
            var view = new EntryListView { ViewModel = observation };
            var window = Show(view, dispatcher);
            try {
                view.GridView.Columns.Add(new GridViewColumn { Header = "Name", Width = 100 });
                DispatcherTools.Pump(dispatcher);
                window.UpdateLayout();

                var header = EntryGridViewportTest.Descendants<GridViewColumnHeader>(view.ItemsListView)
                    .First(item => item.Column is not null);
                var binding = BindingOperations.GetBindingExpression(header, ButtonBase.CommandProperty);

                Assert.That(binding, Is.Not.Null);
                Assert.Multiple(() => {
                    Assert.That(binding.Status, Is.EqualTo(BindingStatus.Active));
                    Assert.That(binding.ResolvedSource, Is.SameAs(observation));
                    Assert.That(binding.ResolvedSourcePropertyName, Is.EqualTo("SortCommand"));
                });
            }
            finally {
                window.Close();
            }
        });
    }

    private sealed class TestProvider : Provider<TestEntry, TestConfig> {
        internal TestProvider() : base("entry-list-view-test") {
        }
    }

    private sealed class TestConfig : EntryConfig {
    }

    private sealed class TestEntry : Entry<TestProvider> {
        public override string ID => "entry";

        public override string Name => "entry";

        public TestEntry(TestProvider provider) : base(provider) {
        }
    }
}
