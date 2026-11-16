using Brows.Commands;
using Brows.Gui;
using Brows.Triggers;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
[NonParallelizable]
internal sealed class CommandSuggestionCollectionControlTest {
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

    private static Exception Capture(Action action) {
        try {
            action();
            return null;
        }
        catch (Exception exception) {
            return exception;
        }
    }

    private static ResourceDictionary LoadStyles() {
        return new ResourceDictionary {
            Source = new Uri(
                "/Brows.Commander.Windows;component/Resources/Styles/" +
                "Brows.Style.CommandSuggestionCollection.xaml",
                UriKind.Relative)
        };
    }

    private static void ProcessDispatcher() {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();
        _ = dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static ICommandSuggestion Suggestion(string input) {
        var suggestion = new Mock<ICommandSuggestion>();
        suggestion.SetupGet(value => value.Group).Returns("Commands");
        suggestion.SetupGet(value => value.GroupOrder).Returns(0);
        suggestion.SetupGet(value => value.Relevance).Returns(0);
        suggestion.SetupGet(value => value.Input).Returns(input);
        suggestion.SetupGet(value => value.Help).Returns(input);
        return suggestion.Object;
    }

    [Test]
    public void ViewModel_LoadedViewConnectsCurrentSuggestionController() {
        var window = new Window {
            Height = 160,
            Left = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            Width = 400
        };
        try {
            window.Resources.MergedDictionaries.Add(LoadStyles());

            var collection = new CommandSuggestionCollection();
            var first = Suggestion("a");
            var second = Suggestion("b");
            collection.Add(first);
            collection.Add(second);

            var control = new CommandSuggestionCollectionControl {
                ViewModel = collection
            };
            window.Content = control;
            window.Show();
            window.UpdateLayout();
            ProcessDispatcher();

            collection.MoveCurrentSuggestion(PressKey.Down);
            collection.MoveCurrentSuggestion(PressKey.Down);
            ProcessDispatcher();

            Assert.That(collection.CurrentSuggestion, Is.SameAs(second));
        }
        finally {
            window.Close();
        }
    }

    [Test]
    public void MoveCurrentSuggestion_ScrollsSelectedItemIntoView() {
        var window = new Window {
            Height = 180,
            Left = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            Width = 400
        };
        try {
            window.Resources.MergedDictionaries.Add(LoadStyles());
            var collection = new CommandSuggestionCollection();
            for (var index = 0; index < 40; index++) {
                collection.Add(Suggestion($"Command {index:D2}"));
            }
            var control = new CommandSuggestionCollectionControl { ViewModel = collection };
            window.Content = control;
            window.Show();
            window.UpdateLayout();
            ProcessDispatcher();
            var list = (ListView)control.Template.FindName("PART_Items", control);
            var scroll = list.FindVisualChild<ScrollViewer>();
            Assert.That(scroll.ScrollableHeight, Is.GreaterThan(0),
                $"List={list.ActualHeight}, extent={scroll.ExtentHeight}, viewport={scroll.ViewportHeight}, items={list.Items.Count}");

            foreach (var key in new[] { PressKey.Down, PressKey.Up }) {
                for (var index = 0; index < 40; index++) {
                    collection.MoveCurrentSuggestion(key);
                    ProcessDispatcher();
                    window.UpdateLayout();
                    var item = (ListViewItem)list.ItemContainerGenerator.ContainerFromItem(
                        collection.CurrentSuggestion);
                    Assert.That(item, Is.Not.Null);
                    var bounds = item.TransformToAncestor(scroll).TransformBounds(
                        new Rect(0, 0, item.ActualWidth, item.ActualHeight));
                    using (Assert.EnterMultipleScope()) {
                        Assert.That(bounds.Top, Is.GreaterThanOrEqualTo(-1));
                        Assert.That(bounds.Bottom, Is.LessThanOrEqualTo(scroll.ActualHeight + 1));
                    }
                }
                Assert.That(list.SelectedIndex, Is.EqualTo(key == PressKey.Down ? 39 : 0));
            }
        }
        finally {
            window.Close();
        }
    }

    [Test]
    public void ItemsSource_InitialAttachmentAndReplacementRegisterBeforeViewCreation() {
        var initialSource = new SubscribeAndMutateCollection();
        var initialListView = new CommandSuggestionItemsListView();
        var initialAttachmentException = Capture(() => BindingOperations.SetBinding(
            initialListView,
            ItemsControl.ItemsSourceProperty,
            new Binding { Source = initialSource }));

        var replacementListView = new CommandSuggestionItemsListView();
        BindingOperations.SetBinding(
            replacementListView,
            ItemsControl.ItemsSourceProperty,
            new Binding { Source = new ObservableCollection<object>() });
        var replacementSource = new SubscribeAndMutateCollection();
        var replacementException = Capture(() => BindingOperations.SetBinding(
            replacementListView,
            ItemsControl.ItemsSourceProperty,
            new Binding { Source = replacementSource }));

        ProcessDispatcher();

        Assert.Multiple(() => {
            Assert.That(initialAttachmentException, Is.Null);
            if (initialAttachmentException is null) {
                Assert.That(initialListView.Items.Cast<object>().Single(), Is.EqualTo("Background item"));
            }
            Assert.That(replacementException, Is.Null);
            if (replacementException is null) {
                Assert.That(replacementListView.Items.Cast<object>().Single(), Is.EqualTo("Background item"));
            }
        });
    }
}
