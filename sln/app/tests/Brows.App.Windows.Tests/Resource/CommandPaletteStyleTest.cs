using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Brows.Resource;

[TestFixture]
internal sealed class CommandPaletteStyleTest {
    private static Color RenderSelectedSuggestion(ListViewItem item, bool rightEdge) {
        var list = (ListView)ItemsControl.ItemsControlFromItemContainer(item);
        var width = (int)Math.Ceiling(list.ActualWidth);
        var height = (int)Math.Ceiling(list.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen()) {
            drawing.DrawRectangle(new VisualBrush(list), null, new Rect(0, 0, width, height));
        }
        bitmap.Render(visual);
        var center = item.TransformToAncestor(list).Transform(new Point(0, item.ActualHeight / 2));
        var pixel = new Int32Rect(rightEdge ? width - 1 : 0, (int)center.Y, 1, 1);
        var pixels = new byte[4];
        bitmap.CopyPixels(pixel, pixels, pixels.Length, 0);
        return Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
    }

    private static ResourceDictionary Load(string assembly, string name) {
        return new ResourceDictionary {
            Source = new Uri($"pack://application:,,,/{assembly};component/{name}.xaml")
        };
    }

    private static void AssertRenderedSelection(Window window, ListViewItem item) {
        var expected = (SolidColorBrush)window.FindResource("Brows.Theme.Selection");
        var paletteBorder = item.Template.FindName("CommandPalette_ListViewItem_Border", item);
        Assert.Multiple(() => {
            Assert.That(expected.IsFrozen, Is.True);
            Assert.That(paletteBorder, Is.TypeOf<Border>());
            Assert.That(RenderSelectedSuggestion(item, rightEdge: false), Is.EqualTo(expected.Color));
            Assert.That(RenderSelectedSuggestion(item, rightEdge: true), Is.EqualTo(expected.Color));
        });
    }

    [TestCase("Light", "Dark")]
    [TestCase("Dark", "Light")]
    public void SelectedSuggestion_UsesActiveSelectionWhileInputRetainsFocus(
        string initialMode,
        string switchedMode) {
        DispatcherTools.Run(dispatcher => {
            var window = new Window {
                Height = 160,
                Left = -10000,
                ShowActivated = false,
                ShowInTaskbar = false,
                Width = 400
            };
            try {
                window.Resources.MergedDictionaries.Add(
                    Load("Brows.Framework.Windows", $"Resources/Themes/{initialMode}.FileExplorer"));
                window.Resources.MergedDictionaries.Add(
                    Load("Brows.Commander.Windows", "Resources/Styles/Brows.Style.CommandPalette"));

                var input = new TextBox {
                    Text = "Command",
                    Width = 320
                };
                var suggestions = new ListView {
                    Height = 48,
                    ItemsSource = new[] { "Current suggestion" },
                    SelectedIndex = 0,
                    Style = (Style)window.FindResource("Brows.Style.CommandPalette.ListView"),
                    Width = 320
                };
                var panel = new StackPanel();
                panel.Children.Add(input);
                panel.Children.Add(suggestions);
                window.Content = panel;
                window.Show();
                window.UpdateLayout();
                Keyboard.Focus(input);
                window.UpdateLayout();

                var item = (ListViewItem)suggestions.ItemContainerGenerator.ContainerFromIndex(0);
                Assert.That(item, Is.Not.Null);
                item.ApplyTemplate();
                suggestions.UpdateLayout();
                window.UpdateLayout();

                Assert.That(Keyboard.FocusedElement, Is.SameAs(input));
                Assert.That(item.IsSelected, Is.True);
                Assert.That(Selector.GetIsSelectionActive(item), Is.False);
                AssertRenderedSelection(window, item);

                window.Resources.MergedDictionaries[0] =
                    Load("Brows.Framework.Windows", $"Resources/Themes/{switchedMode}.FileExplorer");
                window.UpdateLayout();

                Assert.That(Keyboard.FocusedElement, Is.SameAs(input));
                Assert.That(item.IsSelected, Is.True);
                AssertRenderedSelection(window, item);
            }
            finally {
                window.Close();
            }
        });
    }
}
