using Brows.Entries;
using Brows.Gui;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Brows.Providers;

[TestFixture]
internal sealed class ProviderContentTest {
    private static void AssertNavigationLayout(Grid grid, bool enabled) {
        var column = grid.ColumnDefinitions[0];
        var expectedWidth = enabled ? new GridLength(0.25, GridUnitType.Star) : new GridLength(0);
        var splitter = grid.Children.OfType<GridSplitter>().Single();
        var detailsGrid = grid.Children.OfType<Grid>().Single();
        var detailsSplitter = detailsGrid.Children.OfType<GridSplitter>().Single();

        Assert.That(column.Width, Is.EqualTo(expectedWidth));
        Assert.That(column.MinWidth, Is.EqualTo(enabled ? 25 : 0));
        Assert.That(column.MaxWidth, Is.EqualTo(enabled ? double.PositiveInfinity : 0));
        Assert.That(splitter.Visibility, Is.EqualTo(enabled ? Visibility.Visible : Visibility.Collapsed));
        Assert.That(splitter.ResizeDirection, Is.EqualTo(GridResizeDirection.Columns));
        Assert.That(splitter.Width, Is.EqualTo(5));
        Assert.That(detailsSplitter.ResizeDirection, Is.EqualTo(GridResizeDirection.Rows));
        Assert.That(detailsSplitter.Height, Is.EqualTo(5));
    }

    [Test]
    public void Constructor_LayoutWrapper_IsNotFocusableOrTabStop() {
        DispatcherTools.Run(dispatcher => {
            var control = new ProviderContent();
            ProviderTestResources.MergeInto(control);
            control.ApplyTemplate();

            Assert.That(control.Focusable, Is.False);
            Assert.That(control.IsTabStop, Is.False);

            var grid = (Grid)VisualTreeHelper.GetChild(control, 0);
            var treeControl = grid.Children.OfType<ProviderTreeView>().Single();
            treeControl.ApplyTemplate();
            var tree = (TreeView)treeControl.Template.FindName("PART_Tree", treeControl);
            Assert.That(tree.Focusable, Is.True);
            Assert.That(tree.IsTabStop, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Provider_NavigationChanges_UpdatesColumnAndSplitter(bool enabledInitially) {
        DispatcherTools.Run(dispatcher => {
            var navigation = new Mock<ProviderNavigation>(Mock.Of<ProviderNavigationNode>()).Object;
            using var provider = new TestProvider {
                Navigation = enabledInitially ? navigation : null
            };
            var control = new ProviderContent { Provider = provider };
            ProviderTestResources.MergeInto(control);
            control.ApplyTemplate();
            DispatcherTools.Pump(dispatcher);

            var grid = (Grid)VisualTreeHelper.GetChild(control, 0);
            AssertNavigationLayout(grid, enabledInitially);

            provider.Navigation = enabledInitially ? null : navigation;
            DispatcherTools.Pump(dispatcher);
            AssertNavigationLayout(grid, !enabledInitially);

            provider.Navigation = enabledInitially ? navigation : null;
            DispatcherTools.Pump(dispatcher);
            AssertNavigationLayout(grid, enabledInitially);
        });
    }

    [Test]
    public void ActiveDetail_UsesRegisteredTemplatesAndCollapsesOnlyWhenEmpty() {
        DispatcherTools.Run(dispatcher => {
            using var provider = new TestProvider();
            var control = new ProviderContent { Provider = provider };
            ProviderTestResources.MergeInto(control);
            var firstTemplate = new DataTemplate();
            var secondTemplate = new DataTemplate();
            var thirdTemplate = new DataTemplate();
            control.Resources.Add(typeof(FirstDetail), firstTemplate);
            control.Resources.Add(typeof(SecondDetail), secondTemplate);
            control.Resources.Add(typeof(ThirdDetail), thirdTemplate);
            control.ApplyTemplate();
            var grid = (Grid)VisualTreeHelper.GetChild(control, 0);
            var detailGrid = grid.Children.OfType<Grid>().Single();
            var splitter = detailGrid.Children.OfType<GridSplitter>().Single();
            var lowerGrid = detailGrid.Children.OfType<Grid>().Single();
            var content = lowerGrid.Children.OfType<ContentControl>().Single();
            ProviderDetail[] details = [new FirstDetail(provider), new SecondDetail(provider), new ThirdDetail(provider)];
            DataTemplate[] templates = [firstTemplate, secondTemplate, thirdTemplate];
            for (var i = 0; i < details.Length; i++) {
                Assert.That(provider.Detail.ShowAsync(i, details[i], CancellationToken.None).GetAwaiter().GetResult(),
                    Is.True);
                DispatcherTools.Pump(dispatcher);
                Assert.That(content.Content, Is.SameAs(details[i]));
                Assert.That(content.ContentTemplateSelector, Is.SameAs(control.DetailTemplateSelector));
                Assert.That(control.DetailTemplateSelector.SelectTemplate(details[i], content), Is.SameAs(templates[i]));
                Assert.That(splitter.Visibility, Is.EqualTo(Visibility.Visible));
                Assert.That(detailGrid.RowDefinitions[2].MinHeight, Is.EqualTo(25));
            }
            provider.Detail.CloseAsync(details[2], CancellationToken.None).GetAwaiter().GetResult();
            DispatcherTools.Pump(dispatcher);
            Assert.That(content.Content, Is.Null);
            Assert.That(splitter.Visibility, Is.EqualTo(Visibility.Collapsed));
            Assert.That(detailGrid.RowDefinitions[2].Height, Is.EqualTo(new GridLength(0)));
        });
    }

    [Test]
    public void Controller_LoadUnloadReattachUsesExpectedOwner() {
        DispatcherTools.Run(_ => {
            using var provider = new TestProvider();
            var first = new ProviderContent { Provider = provider };
            var second = new ProviderContent { Provider = provider };
            var root = new Grid();
            root.Children.Add(first);
            var window = new Window {
                Content = root, ShowActivated = false, ShowInTaskbar = false,
                Left = -10000, Width = 200, Height = 100
            };
            try {
                window.Show();
                window.UpdateLayout();
                DispatcherTools.Pump(window.Dispatcher);
                Assert.That(provider.Detail.Controller, Is.SameAs(first));
                root.Children.Add(second);
                window.UpdateLayout();
                DispatcherTools.Pump(window.Dispatcher);
                Assert.That(provider.Detail.Controller, Is.SameAs(second));
                root.Children.Remove(first);
                DispatcherTools.Pump(window.Dispatcher);
                Assert.That(provider.Detail.Controller, Is.SameAs(second));
                root.Children.Remove(second);
                DispatcherTools.Pump(window.Dispatcher);
                Assert.That(provider.Detail.Controller, Is.Null);
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void ShowAsync_FromWorkerPublishesActiveOnHostDispatcher() {
        DispatcherTools.Run(dispatcher => {
            using var provider = new TestProvider();
            var control = new ProviderContent { Provider = provider };
            var window = new Window {
                Content = control, Left = -10000, Width = 200, Height = 100,
                ShowActivated = false, ShowInTaskbar = false
            };
            try {
                window.Show();
                DispatcherTools.Pump(dispatcher);
                var changedOnDispatcher = false;
                provider.Detail.PropertyChanged += (_, args) => {
                    if (args.PropertyName == nameof(ProviderDetailSet.Active)) {
                        changedOnDispatcher = dispatcher.CheckAccess();
                    }
                };
                var detail = new FirstDetail(provider);
                var work = Task.Run(() => provider.Detail.ShowAsync("first", detail, CancellationToken.None));
                var frame = new DispatcherFrame();
                var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timeout.Tick += (_, _) => frame.Continue = false;
                timeout.Start();
                _ = work.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
                Dispatcher.PushFrame(frame);
                timeout.Stop();
                Assert.That(work.IsCompleted, Is.True, "The host did not dispatch the view transition.");
                Assert.That(work.GetAwaiter().GetResult(), Is.True);
                Assert.That(changedOnDispatcher, Is.True);
                Assert.That(provider.Detail.Active, Is.SameAs(detail));
            }
            finally {
                window.Close();
            }
        });
    }
    private sealed class FirstDetail : ProviderDetail {
        internal FirstDetail(Provider provider) : base(provider) {
        }

        protected override ProviderDetail Clone(Provider provider) {
            return null;
        }
    }
    private sealed class SecondDetail : ProviderDetail {
        internal SecondDetail(Provider provider) : base(provider) {
        }

        protected override ProviderDetail Clone(Provider provider) {
            return null;
        }
    }
    private sealed class ThirdDetail : ProviderDetail {
        internal ThirdDetail(Provider provider) : base(provider) {
        }

        protected override ProviderDetail Clone(Provider provider) {
            return null;
        }
    }
    private sealed class TestProvider : Provider<Entry, EntryConfig> {
        internal TestProvider() : base("test") {
        }
    }
}
