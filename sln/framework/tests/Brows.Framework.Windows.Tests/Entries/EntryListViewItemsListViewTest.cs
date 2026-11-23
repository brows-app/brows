using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryListViewItemsListViewTest {
    private static EntryListViewItemsListView CreateList(ObservableCollection<string> items) {
        var grid = new EntryGridView();
        grid.Columns.Add(new GridViewColumn { Width = 100, Header = "Name" });
        return new EntryListViewItemsListView {
            IsSynchronizedWithCurrentItem = true,
            ItemsSource = items,
            View = grid
        };
    }

    private static Window Show(EntryListViewItemsListView list, Dispatcher dispatcher) {
        var window = EntryGridViewportTest.CreateWindow(list);
        window.Show();
        window.Activate();
        DispatcherTools.Pump(dispatcher);
        return window;
    }

    private static object FocusedItem() {
        var focused = Keyboard.FocusedElement as ListViewItem;
        return focused?.DataContext;
    }

    [Test]
    public void Focus_BeforeEntriesLoad_MovesFocusToTheEntryWhenItLoads() {
        DispatcherTools.Run(dispatcher => {
            var items = new ObservableCollection<string>();
            var list = CreateList(items);
            var window = Show(list, dispatcher);
            try {
                list.Focus();
                DispatcherTools.Pump(dispatcher);

                items.Add("only");
                DispatcherTools.Pump(dispatcher);

                Assert.That(FocusedItem(), Is.EqualTo("only"));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Focus_WithEntries_MovesFocusToTheCurrentEntry() {
        DispatcherTools.Run(dispatcher => {
            var items = new ObservableCollection<string> { "first", "second", "third" };
            var list = CreateList(items);
            var window = Show(list, dispatcher);
            try {
                list.Items.MoveCurrentTo("second");

                list.Focus();
                DispatcherTools.Pump(dispatcher);

                Assert.That(FocusedItem(), Is.EqualTo("second"));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Focus_WithCurrentEntryOutOfView_ScrollsToAndFocusesTheCurrentEntry() {
        DispatcherTools.Run(dispatcher => {
            var items = new ObservableCollection<string>();
            for (var i = 0; i < 500; i++) {
                items.Add($"entry{i}");
            }
            var list = CreateList(items);
            VirtualizingPanel.SetIsVirtualizing(list, true);
            var window = Show(list, dispatcher);
            try {
                list.Items.MoveCurrentTo("entry400");
                Assert.That(list.ItemContainerGenerator.ContainerFromItem("entry400"), Is.Null);

                list.Focus();
                DispatcherTools.Pump(dispatcher);

                Assert.That(FocusedItem(), Is.EqualTo("entry400"));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Focus_WithoutEntries_KeepsFocusOnTheList() {
        DispatcherTools.Run(dispatcher => {
            var items = new ObservableCollection<string>();
            var list = CreateList(items);
            var window = Show(list, dispatcher);
            try {
                list.Focus();
                DispatcherTools.Pump(dispatcher);

                Assert.That(list.IsKeyboardFocused, Is.True);
            }
            finally {
                window.Close();
            }
        });
    }
}
