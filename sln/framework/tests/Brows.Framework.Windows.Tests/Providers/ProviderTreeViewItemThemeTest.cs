using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Brows.Providers;

[TestFixture]
internal sealed class ProviderTreeViewItemThemeTest {
    private static Path FindExpandPath(TreeViewItem item) {
        item.ApplyTemplate();
        var expander = (ToggleButton)item.Template.FindName("Expander", item);
        expander.ApplyTemplate();
        return (Path)expander.Template.FindName("ExpandPath", expander);
    }

    private static ResourceDictionary Load(string name) {
        return new ResourceDictionary {
            Source = new Uri($"pack://application:,,,/Brows.Framework.Windows;component/Resources/{name}.xaml")
        };
    }

    private static void AssertBrushMatchesTheme(Brush brush, FrameworkElement resourceOwner, string key) {
        Assert.That(brush, Is.TypeOf<SolidColorBrush>());
        var actual = (SolidColorBrush)brush;
        var expected = (SolidColorBrush)resourceOwner.FindResource(key);
        Assert.That(actual.Color, Is.EqualTo(expected.Color));
    }

    [TestCase("Light", "Dark", TestName = "ExpandPath_UsesMutedBrushAndTracksTheme_LightToDark")]
    [TestCase("Dark", "Light", TestName = "ExpandPath_UsesMutedBrushAndTracksTheme_DarkToLight")]
    public void ExpandPath_UsesMutedBrushAndTracksTheme(string mode, string nextMode) {
        DispatcherTools.Run(dispatcher => {
            var window = new Window { ShowInTaskbar = false, ShowActivated = false, Left = -10000 };
            try {
                window.Resources.MergedDictionaries.Add(Load($"Themes/{mode}.FileExplorer"));
                window.Resources.MergedDictionaries.Add(Load("Resources"));

                var item = new TreeViewItem {
                    Header = "Provider",
                    IsExpanded = true,
                    Style = (Style)window.FindResource("Brows.Style.ProviderTreeViewItem")
                };
                item.Items.Add(new TreeViewItem { Header = "Child" });
                var tree = new TreeView();
                tree.Items.Add(item);
                window.Content = tree;
                window.Show();
                window.UpdateLayout();

                var path = FindExpandPath(item);
                AssertBrushMatchesTheme(path.Fill, window, "Brows.Theme.Muted");
                AssertBrushMatchesTheme(path.Stroke, window, "Brows.Theme.Muted");

                window.Resources.MergedDictionaries[0] = Load($"Themes/{nextMode}.FileExplorer");
                window.UpdateLayout();

                AssertBrushMatchesTheme(path.Fill, window, "Brows.Theme.Muted");
                AssertBrushMatchesTheme(path.Stroke, window, "Brows.Theme.Muted");

                item.IsExpanded = false;
                window.UpdateLayout();

                Assert.That(((SolidColorBrush)path.Fill).Color.A, Is.Zero);
                AssertBrushMatchesTheme(path.Stroke, window, "Brows.Theme.Muted");
            }
            finally {
                window.Close();
            }
        });
    }
}
