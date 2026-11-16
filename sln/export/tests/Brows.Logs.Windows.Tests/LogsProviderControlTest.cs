using Brows.Localization;
using Brows.Providers;
using Domore.Windows.Controls;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace Brows;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
internal sealed class LogsProviderControlTest {
    private LogsProvider Provider;
    private Window Window;

    private static ResourceDictionary Load(string source) {
        return new ResourceDictionary {
            Source = new Uri(source, UriKind.Relative)
        };
    }

    private static T Descendant<T>(DependencyObject parent) where T : DependencyObject {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++) {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) {
                return match;
            }
            var descendant = Descendant<T>(child);
            if (descendant is not null) {
                return descendant;
            }
        }
        return null;
    }

    private Window Show(string theme, object content) {
        Window = new Window {
            Width = 400,
            Height = 300,
            Left = -10000,
            ShowInTaskbar = false
        };
        Window.Resources.MergedDictionaries.Add(Load(
            $"/Brows.Framework.Windows;component/Resources/Themes/{theme}.FileExplorer.xaml"));
        Window.Resources.MergedDictionaries.Add(Load(
            "/Brows.Framework.Windows;component/Resources/Resources.xaml"));
        Window.Resources.MergedDictionaries.Add(Load(
            "/Brows.Logs.Windows;component/Resource/Resource.xaml"));
        Window.Content = content;
        Window.Show();
        Window.UpdateLayout();
        return Window;
    }

    [OneTimeSetUp]
    public void OneTimeSetUp() {
        /*
         * An application registers the pack scheme that resolves the resource dictionaries,
         * and must not shut down when a test closes its window.
         */
        _ = Application.Current ?? new Application {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };
    }

    [SetUp]
    public void SetUp() {
        Provider = new LogsProvider("Logs");
    }

    [TearDown]
    public void TearDown() {
        Window?.Close();
        Window = null;
        Provider?.Dispose();
        Provider = null;
    }

    [Test]
    public void Resource_PanelProviderTemplate_ShowsLogsProviderControl() {
        var resource = Load("/Brows.Logs.Windows;component/Resource/Resource.xaml");
        var template = (DataTemplate)resource["PanelProvider_LogsProvider"];

        Assert.That(template.LoadContent(), Is.TypeOf<LogsProviderControl>());
    }

    [Test]
    public void LogsProviderControl_Shown_HostsLogsControl() {
        var control = new LogsProviderControl { ViewModel = Provider };
        Show("Dark", control);

        Assert.That(Descendant<LogsControl>(control), Is.Not.Null);
    }

    [Test]
    public void LogsProviderControl_Shown_FocusesLogsControlThroughProvider() {
        var control = new LogsProviderControl { ViewModel = Provider };
        Show("Dark", control);
        Window.Activate();

        var focused = ((IProvider)Provider).Focus();

        Assert.That(focused, Is.True);
        Assert.That(Descendant<LogsControl>(control).IsKeyboardFocused, Is.True);
    }

    [TestCase("Dark")]
    [TestCase("Light")]
    public void LogsProviderControl_Shown_StylesLogsControlWithTheme(string theme) {
        var control = new LogsProviderControl { ViewModel = Provider };
        var window = Show(theme, control);
        var logs = Descendant<LogsControl>(control);

        using (Assert.EnterMultipleScope()) {
            Assert.That(((SolidColorBrush)logs.Background).Color, Is.EqualTo(Colors.Transparent));
            Assert.That(logs.Foreground, Is.SameAs(window.FindResource("Brows.Theme.LogsProvider.Foreground")));
            Assert.That(logs.LogDebugForeground,
                        Is.SameAs(window.FindResource("Brows.Theme.LogsProvider.DebugForeground")));
            Assert.That(logs.LogWarnForeground,
                        Is.SameAs(window.FindResource("Brows.Theme.LogsProvider.WarnForeground")));
            Assert.That(logs.LogErrorForeground,
                        Is.SameAs(window.FindResource("Brows.Theme.LogsProvider.ErrorForeground")));
            Assert.That(logs.LogCriticalForeground,
                        Is.SameAs(window.FindResource("Brows.Theme.LogsProvider.CriticalForeground")));
            Assert.That(logs.LogSelectionBackground,
                        Is.SameAs(window.FindResource("Brows.Theme.LogsProvider.SelectionBackground")));
            Assert.That(logs.FontFamily.Source, Is.EqualTo("Consolas"));
            Assert.That(logs.LogScrollViewerStyle,
                        Is.SameAs(window.FindResource("Brows.Style.LogsProvider.ScrollViewer")));
        }
    }

    [Test]
    public void LogsProviderControl_Shown_ShowsTranslatedProviderName() {
        var translation = new Mock<ITranslation>();
        translation.Setup(item => item.Value("Logs")).Returns("Translated logs");
        var previousTranslation = Translation.Global;
        Translation.Global = translation.Object;
        try {
            var control = new LogsProviderControl { ViewModel = Provider };
            Show("Dark", control);
            var grid = Descendant<ProviderGrid>(control);

            Assert.That(grid.Text, Is.EqualTo("Translated logs"));
            Assert.That(grid.Provider, Is.SameAs(Provider));
        }
        finally {
            Translation.Global = previousTranslation;
        }
    }
}
