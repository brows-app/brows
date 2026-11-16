using Brows.Entries;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace Brows.Providers;

[TestFixture]
internal sealed class ProviderGridTest {
    [Test]
    public void Constructor_LayoutWrapper_IsNotFocusableOrTabStop() {
        DispatcherTools.Run(dispatcher => {
            var control = new ProviderGrid();
            ProviderTestResources.MergeInto(control);
            control.ApplyTemplate();

            Assert.That(control.Focusable, Is.False);
            Assert.That(control.IsTabStop, Is.False);
        });
    }

    [Test]
    public void DefaultContent_UsesTheProviderPropertyWhenItIsSet() {
        DispatcherTools.Run(dispatcher => {
            var provider = new TestProvider();
            var grid = new ProviderGrid {
                Provider = provider,
                IconTemplate = new DataTemplate(),
                TextTemplate = new DataTemplate(),
                ContentTemplate = new DataTemplate(),
            };
            ProviderTestResources.MergeInto(grid);
            grid.Style = new Style(typeof(ProviderGrid), (Style)grid.FindResource("Brows.Style.ProviderGrid"));
            try {
                grid.ApplyTemplate();
                DispatcherTools.Pump(dispatcher);
                Assert.That(grid.Provider, Is.SameAs(provider));
                Assert.That(FindContent(grid, provider), Is.Not.Null);
            }
            finally {
                ((IProvider)provider).End();
                provider.Dispose();
            }
        });
    }

    [Test]
    public void DefaultContent_IsEmptyWhenProviderIsUnset() {
        DispatcherTools.Run(dispatcher => {
            var provider = new TestProvider();
            var grid = new ProviderGrid {
                DataContext = provider,
                IconTemplate = new DataTemplate(),
                TextTemplate = new DataTemplate(),
                ContentTemplate = new DataTemplate(),
            };
            ProviderTestResources.MergeInto(grid);
            try {
                grid.ApplyTemplate();
                DispatcherTools.Pump(dispatcher);
                Assert.That(grid.Provider, Is.Null);
                Assert.That(FindContent(grid, provider), Is.Null);
                Assert.That(FindContent(grid, null), Is.Not.Null);
            }
            finally {
                ((IProvider)provider).End();
                provider.Dispose();
            }
        });
    }

    [Test]
    public void DefaultContent_IsEmptyAfterProviderIsCleared() {
        DispatcherTools.Run(dispatcher => {
            using var provider = new TestProvider();
            var grid = new ProviderGrid {
                Provider = provider,
                DataContext = provider,
                IconTemplate = new DataTemplate(),
                TextTemplate = new DataTemplate(),
                ContentTemplate = new DataTemplate(),
            };
            ProviderTestResources.MergeInto(grid);
            grid.ApplyTemplate();
            DispatcherTools.Pump(dispatcher);
            var content = FindContent(grid, provider);
            Assert.That(content, Is.Not.Null);

            grid.Provider = null;
            DispatcherTools.Pump(dispatcher);

            Assert.That(content.Content, Is.Null);
        });
    }

    [Test]
    public void Template_RemovalDoesNotReparentTheContainingControl() {
        DispatcherTools.Run(dispatcher => {
            using var provider = new TestProvider();
            var wrapper = new Control {
                Tag = provider,
                Template = (ControlTemplate)XamlReader.Parse("""
                    <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                     xmlns:p="clr-namespace:Brows.Providers;assembly=Brows.Framework.Windows">
                      <p:ProviderGrid Provider="{Binding Tag, RelativeSource={RelativeSource TemplatedParent}}"
                                      DataContext="{Binding RelativeSource={RelativeSource TemplatedParent}}">
                        <p:ProviderGrid.IconTemplate><DataTemplate/></p:ProviderGrid.IconTemplate>
                        <p:ProviderGrid.TextTemplate><DataTemplate/></p:ProviderGrid.TextTemplate>
                        <p:ProviderGrid.ContentTemplate><DataTemplate/></p:ProviderGrid.ContentTemplate>
                      </p:ProviderGrid>
                    </ControlTemplate>
                    """),
            };
            ProviderTestResources.MergeInto(wrapper);
            wrapper.ApplyTemplate();
            var grid = (ProviderGrid)VisualTreeHelper.GetChild(wrapper, 0);
            grid.ApplyTemplate();
            DispatcherTools.Pump(dispatcher);
            var content = FindContent(grid, provider);
            Assert.That(content, Is.Not.Null);
            Assert.That(grid.DataContext, Is.SameAs(wrapper));

            wrapper.Template = null;
            wrapper.ApplyTemplate();
            DispatcherTools.Pump(dispatcher);

            Assert.That(content.Content, Is.Null);
            Assert.That(wrapper.Parent, Is.Null);
            Assert.That(VisualTreeHelper.GetChildrenCount(wrapper), Is.Zero);
        });
    }

    [Test]
    public void Focus_WithoutEntries_FocusesFirstFocusableElementOfContent() {
        DispatcherTools.Run(dispatcher => {
            using var provider = new ViewProvider();
            var grid = ViewGrid(provider);
            var window = Show(grid, dispatcher);
            try {
                var focused = ((IProvider)provider).Focus();
                var textBox = Find<TextBox>(grid);

                Assert.That(focused, Is.True);
                Assert.That(textBox.IsKeyboardFocused, Is.True);
                Assert.That(((IProvider)provider).Focus(), Is.True);
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Loaded_AttachesAsControllerOfProvider() {
        DispatcherTools.Run(dispatcher => {
            using var provider = new ViewProvider();
            var changes = 0;
            ((IProvider)provider).ControllerChanged += (sender, e) => changes++;
            var grid = ViewGrid(provider);
            var window = Show(grid, dispatcher);
            try {
                Assert.That(changes, Is.EqualTo(1));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Unloaded_DetachesAsControllerOfProvider() {
        DispatcherTools.Run(dispatcher => {
            using var provider = new ViewProvider();
            var grid = ViewGrid(provider);
            var window = Show(grid, dispatcher);
            window.Content = null;
            DispatcherTools.Pump(dispatcher);
            try {
                Assert.That(((IProvider)provider).Focus(), Is.False);
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Provider_WhenReplaced_MovesControllerToReplacement() {
        DispatcherTools.Run(dispatcher => {
            using var provider = new ViewProvider();
            using var replacement = new ViewProvider();
            var grid = ViewGrid(provider);
            var window = Show(grid, dispatcher);
            try {
                grid.Provider = replacement;
                DispatcherTools.Pump(dispatcher);

                Assert.That(((IProvider)provider).Focus(), Is.False);
                Assert.That(((IProvider)replacement).Focus(), Is.True);
            }
            finally {
                window.Close();
            }
        });
    }

    private static ProviderGrid ViewGrid(Provider provider) {
        var grid = new ProviderGrid {
            Provider = provider,
            IconTemplate = new DataTemplate(),
            TextTemplate = new DataTemplate(),
            ContentTemplate = new DataTemplate {
                VisualTree = new FrameworkElementFactory(typeof(TextBox))
            }
        };
        ProviderTestResources.MergeInto(grid);
        return grid;
    }

    private static Window Show(ProviderGrid grid, Dispatcher dispatcher) {
        var window = new Window {
            Content = grid,
            Width = 300,
            Height = 200,
            Left = -10000,
            ShowInTaskbar = false
        };
        window.Show();
        window.Activate();
        DispatcherTools.Pump(dispatcher);
        return window;
    }

    private static T Find<T>(DependencyObject root) where T : DependencyObject {
        if (root is T match) {
            return match;
        }
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++) {
            var found = Find<T>(VisualTreeHelper.GetChild(root, i));
            if (found is not null) {
                return found;
            }
        }
        return null;
    }

    private static ContentControl FindContent(DependencyObject root, Provider provider) {
        if (root is ContentControl content) {
            var contentIsProvider = ReferenceEquals(content.Content, provider);
            if (contentIsProvider) {
                return content;
            }
        }
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++) {
            var found = FindContent(VisualTreeHelper.GetChild(root, i), provider);
            if (found is not null) {
                return found;
            }
        }
        return null;
    }

    private sealed class TestProvider : Provider<TestEntry, TestConfig> {
        internal TestProvider() : base("provider-grid-test") {
        }
    }

    private sealed class ViewProvider : Provider {
        internal ViewProvider() : base("provider-grid-view-test") {
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
