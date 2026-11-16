using Brows.Commands;
using Brows.Gui;
using Brows.Support;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace Brows;

[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class FileSystemFindResultControlTest {
    private static bool ResourcesLoaded;

    private sealed class SubscribeAndMutateCollection : ObservableCollection<object>, ISyncedCollection {
        private readonly object Locker = new();
        private int SubscriptionCount;

        public override event NotifyCollectionChangedEventHandler CollectionChanged {
            add {
                base.CollectionChanged += value;
                if (Interlocked.Exchange(ref SubscriptionCount, 1) != 0) {
                    return;
                }

                var mutation = Task.Run(() => {
                    lock (Locker) {
                        Add("Background item");
                    }
                });
                var completed = mutation.Wait(TimeSpan.FromSeconds(5));
                if (!completed) {
                    throw new TimeoutException("The background collection update did not complete.");
                }
                mutation.GetAwaiter().GetResult();
            }
            remove {
                base.CollectionChanged -= value;
            }
        }

        public void Sync(IEnumerable collection, object context, Action accessMethod, bool writeAccess) {
            lock (Locker) {
                accessMethod();
            }
        }
    }

    private static FileSystemFindResult Result(DirectoryInfo root) {
        return new FileSystemFindResult(root, new FindParameter { Pattern = "*" }, "find", null);
    }

    private static void AddResources() {
        var application = Application.Current ?? new Application();
        if (ResourcesLoaded) {
            return;
        }
        application.Resources.MergedDictionaries.Add(new ResourceDictionary {
            Source = new Uri(
                "/Brows.FileSystem.Commands.Find.Windows;component/Resource/Resource.xaml",
                UriKind.Relative)
        });
        var culture = new ResourceDictionary();
        application.Resources.MergedDictionaries.Add(culture);
        culture.Source = new Uri(
            "/Brows.FileSystem.Commands.Find.Windows;component/Resource/Culture/_.xaml",
            UriKind.Relative);
        ResourcesLoaded = true;
    }

    private static void ProcessDispatcher() {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();
        _ = dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static FileSystemFindResultControl CreateControl(FileSystemFindResult result) {
        var control = new FileSystemFindResultControl {
            DataContext = result
        };
        AddResources();
        control.Measure(new Size(800, 600));
        control.Arrange(new Rect(0, 0, 800, 600));
        control.UpdateLayout();
        ProcessDispatcher();
        return control;
    }

    [Test]
    public void ItemsSource_InitialAttachmentRegistersSynchronizationBeforeViewCreation() {
        var source = new SubscribeAndMutateCollection();
        var listView = new FileSystemFindResultListView();
        BindingOperations.SetBinding(
            listView,
            ItemsControl.ItemsSourceProperty,
            new Binding { Source = source });

        ProcessDispatcher();

        Assert.That(listView.Items.Cast<object>().Single(), Is.EqualTo("Background item"));
    }

    [Test]
    public void ItemsSource_ReplacementRegistersSynchronizationBeforeViewCreation() {
        var listView = new FileSystemFindResultListView();
        BindingOperations.SetBinding(
            listView,
            ItemsControl.ItemsSourceProperty,
            new Binding { Source = new ObservableCollection<object>() });
        var source = new SubscribeAndMutateCollection();

        BindingOperations.SetBinding(
            listView,
            ItemsControl.ItemsSourceProperty,
            new Binding { Source = source });
        ProcessDispatcher();

        Assert.That(listView.Items.Cast<object>().Single(), Is.EqualTo("Background item"));
    }

    [Test]
    public void Status_UsesLocalizedStateAndShowsPartialOutcomes() {
        using var fixture = new TempDirectory();
        fixture.CreateFile("status.txt");
        var result = Result(fixture.Root);
        var control = CreateControl(result);
        var summary = control.Children
            .OfType<WrapPanel>()
            .Single();
        var status = (TextBlock)summary.Children[0];
        var exception = (TextBlock)summary.Children[1];
        var truncated = (TextBlock)summary.Children[2];
        var skippedCount = (TextBlock)((StackPanel)summary.Children[3]).Children[1];
        var errorCount = (TextBlock)((StackPanel)summary.Children[4]).Children[1];

        Assert.That(status.Text, Is.EqualTo("Searching"));

        result.Complete = true;
        ProcessDispatcher();
        Assert.That(status.Text, Is.EqualTo("Search completed."));

        result.Exception = new IOException("The selected root is unavailable.");
        ProcessDispatcher();
        Assert.That(status.Text, Is.EqualTo("Failed. Partial results may be shown:"));
        Assert.That(exception.Text, Is.EqualTo("The selected root is unavailable."));
        Assert.That(exception.Visibility, Is.EqualTo(Visibility.Visible));

        result.Canceled = true;
        ProcessDispatcher();
        Assert.That(status.Text, Is.EqualTo("Canceled. Partial results may be shown."));
        Assert.That(exception.Visibility, Is.EqualTo(Visibility.Collapsed));

        result.Truncated = true;
        result.SkippedCount = 2;
        result.ErrorCount = 1;
        ProcessDispatcher();
        Assert.That(truncated.Visibility, Is.EqualTo(Visibility.Visible));
        Assert.That(skippedCount.Text, Is.EqualTo("2"));
        Assert.That(((StackPanel)summary.Children[3]).Visibility, Is.EqualTo(Visibility.Visible));
        Assert.That(errorCount.Text, Is.EqualTo("1"));
        Assert.That(((StackPanel)summary.Children[4]).Visibility, Is.EqualTo(Visibility.Visible));
    }

    [Test]
    public void ResetAndReopen_PreserveCurrentItemCurrency() {
        using var fixture = new TempDirectory();
        var first = fixture.CreateFile("first.txt");
        var selected = fixture.CreateFile(Path.Combine("nested", "selected.txt"));
        var later = fixture.CreateFile("later.txt");
        var result = Result(fixture.Root);
        result.Add(new FoundInInfo[] {
            new(first, fixture.Root),
            new(selected, fixture.Root)
        });

        var originalControl = CreateControl(result);
        var originalItems = (ListView)originalControl.FindName("ItemsControl");
        var originalController = new FileSystemFindResultController(originalControl);
        var controlled = (IFileSystemFindResultController)originalController;
        ((IControlled<IFileSystemFindResultController>)result).Controller = controlled;
        ProcessDispatcher();

        var selectedRow = ((IEnumerable)result.Source)
            .Cast<FoundInInfo>()
            .Single(row => row.FullPath == selected.FullName);
        var originalView = (ICollectionView)originalItems.Items;
        Assert.That(originalView.MoveCurrentTo(selectedRow), Is.True);
        ProcessDispatcher();
        Assert.That(result.CurrentItem, Is.SameAs(selectedRow));

        Task.Run(() => result.Add(new FoundInInfo[] { new(later, fixture.Root) }))
            .GetAwaiter()
            .GetResult();
        ProcessDispatcher();
        Assert.That(originalView.CurrentItem, Is.SameAs(selectedRow));
        Assert.That(result.CurrentItem, Is.SameAs(selectedRow));

        ((IControlled<IFileSystemFindResultController>)result).Controller = null;
        Assert.That(result.CurrentItem, Is.Null);

        var reopenedControl = CreateControl(result);
        var reopenedItems = (ListView)reopenedControl.FindName("ItemsControl");
        var reopenedController = new FileSystemFindResultController(reopenedControl);
        ((IControlled<IFileSystemFindResultController>)result).Controller = reopenedController;
        ProcessDispatcher();

        var reopenedView = (ICollectionView)reopenedItems.Items;
        Assert.That(reopenedView.CurrentItem, Is.SameAs(selectedRow));
        Assert.That(result.CurrentItem, Is.SameAs(selectedRow));
    }
}
