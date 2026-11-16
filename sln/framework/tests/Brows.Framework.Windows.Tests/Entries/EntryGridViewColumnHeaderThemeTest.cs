using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryGridViewColumnHeaderThemeTest {
    private static EntryGridViewColumnHeader AddHeader(Window window) {
        var header = new EntryGridViewColumnHeader { Display = "Name" };
        var view = new GridView {
            ColumnHeaderContainerStyle = (Style)window.FindResource("Brows.Style.EntryGridViewColumnHeader")
        };
        view.Columns.Add(new GridViewColumn {
            Header = header,
            Width = 200,
            DisplayMemberBinding = new Binding(".")
        });
        var list = new ListView { View = view };
        list.Items.Add("Entry");
        window.Content = list;
        return header;
    }

    private static GridViewColumnHeader FindHeaderContainer(EntryGridViewColumnHeader header) {
        DependencyObject current = header;
        while (current is not null && current is not GridViewColumnHeader) {
            current = VisualTreeHelper.GetParent(current);
        }
        return current as GridViewColumnHeader;
    }

    private static void AssertBrushMatchesTheme(Brush brush, FrameworkElement resourceOwner, string key) {
        Assert.That(brush, Is.TypeOf<SolidColorBrush>());
        var actual = (SolidColorBrush)brush;
        var expected = (SolidColorBrush)resourceOwner.FindResource(key);
        Assert.That(actual.Color, Is.EqualTo(expected.Color));
    }

    private static TextBlock FindText(DependencyObject parent, string text) {
        if (parent is TextBlock block && block.Text == text) {
            return block;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
            var found = FindText(VisualTreeHelper.GetChild(parent, i), text);
            if (found is not null) {
                return found;
            }
        }
        return null;
    }

    private static ResourceDictionary Load(string name) {
        return new ResourceDictionary {
            Source = new Uri($"pack://application:,,,/Brows.Framework.Windows;component/Resources/{name}.xaml")
        };
    }

    private static Rectangle FindGripperRectangle(GridViewColumnHeader header) {
        header.ApplyTemplate();
        var gripper = (Thumb)header.Template.FindName("PART_HeaderGripper", header);
        gripper.ApplyTemplate();
        return FindRectangle(gripper);
    }

    private static Rectangle FindRectangle(DependencyObject parent) {
        if (parent is Rectangle rectangle) {
            return rectangle;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
            var found = FindRectangle(VisualTreeHelper.GetChild(parent, i));
            if (found is not null) {
                return found;
            }
        }
        return null;
    }

    private static void SetReadOnlyProperty(
        DependencyObject element,
        Type ownerType,
        string propertyName,
        object value) {
        var field = ownerType.GetField(
            $"{propertyName}PropertyKey",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(field, Is.Not.Null, $"Could not find {ownerType.Name}.{propertyName}PropertyKey.");
        var key = (DependencyPropertyKey)field.GetValue(null);
        element.SetValue(key, value);
    }

    private static Window CreateWindow(string mode) {
        var window = new Window { ShowInTaskbar = false, ShowActivated = false, Left = -10000 };
        window.Resources.MergedDictionaries.Add(Load($"Themes/{mode}.FileExplorer"));
        window.Resources.MergedDictionaries.Add(Load("Resources"));
        return window;
    }

    [TestCase("Light", "Dark", TestName = "HeaderDivider_UsesThemeBorderInAllStates_LightToDark")]
    [TestCase("Dark", "Light", TestName = "HeaderDivider_UsesThemeBorderInAllStates_DarkToLight")]
    public void HeaderDivider_UsesThemeBorderInAllStates(string mode, string nextMode) {
        DispatcherTools.Run(dispatcher => {
            var window = CreateWindow(mode);
            try {
                var header = AddHeader(window);
                window.Show();
                window.UpdateLayout();
                var headerContainer = FindHeaderContainer(header);
                Assert.That(headerContainer, Is.Not.Null);

                var divider = FindGripperRectangle(headerContainer);
                Assert.That(divider, Is.Not.Null);
                AssertBrushMatchesTheme(divider.Fill, window, "Brows.Theme.Border");

                SetReadOnlyProperty(headerContainer, typeof(UIElement), "IsMouseOver", true);
                window.UpdateLayout();
                AssertBrushMatchesTheme(divider.Fill, window, "Brows.Theme.Border");
                Assert.That(divider.IsVisible, Is.True);

                window.Resources.MergedDictionaries[0] = Load($"Themes/{nextMode}.FileExplorer");
                window.UpdateLayout();
                AssertBrushMatchesTheme(divider.Fill, window, "Brows.Theme.Border");
                Assert.That(divider.IsVisible, Is.True);

                SetReadOnlyProperty(headerContainer, typeof(ButtonBase), "IsPressed", true);
                window.UpdateLayout();
                var gripper = (Thumb)headerContainer.Template.FindName("PART_HeaderGripper", headerContainer);
                Assert.That(gripper.Visibility, Is.EqualTo(Visibility.Hidden));
            }
            finally {
                window.Close();
            }
        });
    }

    [TestCase("Light", "Dark", TestName = "DisabledHeader_UsesMutedBrushAndTracksTheme_LightToDark")]
    [TestCase("Dark", "Light", TestName = "DisabledHeader_UsesMutedBrushAndTracksTheme_DarkToLight")]
    public void DisabledHeader_UsesMutedBrushAndTracksTheme(string mode, string nextMode) {
        DispatcherTools.Run(dispatcher => {
            var window = CreateWindow(mode);
            try {
                var header = AddHeader(window);
                window.Show();
                window.UpdateLayout();
                var headerContainer = FindHeaderContainer(header);
                Assert.That(headerContainer, Is.Not.Null);
                headerContainer.IsEnabled = false;
                window.UpdateLayout();

                var text = FindText(header, "Name");
                Assert.That(text, Is.Not.Null);
                AssertBrushMatchesTheme(text.Foreground, window, "Brows.Theme.Muted");

                window.Resources.MergedDictionaries[0] = Load($"Themes/{nextMode}.FileExplorer");
                window.UpdateLayout();

                AssertBrushMatchesTheme(text.Foreground, window, "Brows.Theme.Muted");
            }
            finally {
                window.Close();
            }
        });
    }
}