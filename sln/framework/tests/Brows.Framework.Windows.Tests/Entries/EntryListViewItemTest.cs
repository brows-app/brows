using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryListViewItemTest {
    private static byte[] Render(Grid row, Int32Rect area) {
        row.Measure(new Size(320, 40));
        row.Arrange(new Rect(0, 0, 320, 40));
        row.UpdateLayout();
        var bitmap = new RenderTargetBitmap(320, 40, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(row);
        var stride = area.Width * 4;
        var pixels = new byte[stride * area.Height];
        bitmap.CopyPixels(area, pixels, stride, 0);
        return pixels;
    }

    [TestCase("Light", "#FF0078D4")]
    [TestCase("Dark", "#FFD0D0D0")]
    public void FocusVisual_PreservesRowContentsAndOutlinesFocus(string mode, string focusColor) {
        DispatcherTools.Run(dispatcher => {
            var row = new Grid();
            row.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri(
                    $"pack://application:,,,/Brows.Framework.Windows;component/Resources/Themes/" +
                    $"{mode}.FileExplorer.xaml")
            });
            row.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri(
                    "pack://application:,,,/Brows.Framework.Windows;component/Resources/Resources.xaml")
            });
            var item = new EntryListViewItemContainer {
                Style = (Style)row.FindResource("Brows.Style.EntryListViewItem")
            };
            var focus = new Control {
                Style = item.FocusVisualStyle,
                IsHitTestVisible = false,
                Visibility = Visibility.Hidden
            };
            var text = new TextBlock {
                Text = "Readable entry.txt",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 16,
                Margin = new Thickness(8),
                Foreground = (Brush)row.FindResource("Brows.Theme.Foreground")
            };
            row.Children.Add(new Border {
                Background = (Brush)row.FindResource("Brows.Theme.Selection"),
                Child = text
            });
            row.Children.Add(focus);
            var interior = new Int32Rect(4, 4, 312, 32);
            var outline = new Int32Rect(0, 0, 320, 1);
            var originalContents = Render(row, interior);
            var originalOutline = Render(row, outline);

            focus.Visibility = Visibility.Visible;
            focus.ApplyTemplate();

            Assert.That(Render(row, interior), Is.EqualTo(originalContents));
            Assert.That(Render(row, outline), Is.Not.EqualTo(originalOutline));
            var color = (Color)ColorConverter.ConvertFromString(focusColor);
            Assert.That(Render(row, new Int32Rect(0, 0, 1, 1)),
                Is.EqualTo(new byte[] { color.B, color.G, color.R, color.A }));
        });
    }
}