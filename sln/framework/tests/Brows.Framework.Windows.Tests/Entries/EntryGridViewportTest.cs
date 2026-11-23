using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryGridViewportTest {
    internal static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T value) {
                yield return value;
            }
            foreach (var descendant in Descendants<T>(child)) {
                yield return descendant;
            }
        }
    }

    internal static Window CreateWindow(FrameworkElement content, string mode = "Dark") {
        var window = new Window {
            Width = 500, Height = 260, ShowInTaskbar = false, ShowActivated = false,
            Left = -10000, Content = content
        };
        window.Resources.MergedDictionaries.Add(new ResourceDictionary {
            Source = new Uri($"pack://application:,,,/Brows.Framework.Windows;component/Resources/Themes/" +
                $"{mode}.FileExplorer.xaml")
        });
        window.Resources.MergedDictionaries.Add(new ResourceDictionary {
            Source = new Uri("pack://application:,,,/Brows.Framework.Windows;component/Resources/Resources.xaml")
        });
        return window;
    }

    [TestCase("Light", 0, 0, 0)]
    [TestCase("Dark", 10, 0, 1)]
    [TestCase("Dark", 17, 9, 100)]
    public void Viewport_ExcludesPaddingAndScrollbars(string mode, double left, double right, int count) {
        DispatcherTools.Run(dispatcher => {
            var list = new EntryListViewItemsListView { Padding = new Thickness(left, 0, right, 0) };
            var window = CreateWindow(list, mode);
            try {
                var grid = new EntryGridView();
                grid.Columns.Add(new GridViewColumn { Width = 100, Header = "Name" });
                list.View = grid;
                list.ItemsSource = Enumerable.Range(0, count).Select(_ => Mock.Of<IEntry>()).ToArray();
                window.Show();
                DispatcherTools.Pump(dispatcher);
                window.UpdateLayout();
                var outer = Descendants<ScrollViewer>(list).First();
                var body = (ScrollContentPresenter)outer.Template.FindName("PART_ScrollContentPresenter", outer);
                var header = Descendants<GridViewHeaderRowPresenter>(outer).Single();
                var rows = Descendants<GridViewRowPresenter>(list).ToArray();
                TestContext.Out.WriteLine($"Runtime={Environment.Version}; padding={list.Padding}; " +
                    $"outer={outer.ActualWidth}; body={body.ActualWidth}; header={header.ActualWidth}; " +
                    $"headerMargin={header.Margin}; extent={outer.ExtentWidth}; rows={rows.Length}");
                Assert.That(body, Is.Not.Null);
                Assert.That(outer.Style, Is.SameAs(outer.FindResource(GridView.GridViewScrollViewerStyleKey)));
                var bar = (ScrollBar)outer.Template.FindName("PART_VerticalScrollBar", outer);
                var gutter = bar.IsVisible ? bar.ActualWidth : 0;
                Assert.That(body.ActualWidth, Is.EqualTo(outer.ActualWidth - left - right - gutter).Within(1));
                if (rows.Length > 0) {
                    var origin = rows[0].TranslatePoint(new Point(), body);
                    TestContext.Out.WriteLine($"rowOrigin={origin}; rowDesired={rows[0].DesiredSize}");
                    Assert.That(origin.X, Is.EqualTo(2).Within(1));
                }
                grid.Columns[0].Width = body.ActualWidth - 4;
                window.UpdateLayout();
                Assert.That(outer.ScrollableWidth, Is.LessThanOrEqualTo(1));
                if (count > 0) {
                    grid.Columns[0].Width = body.ActualWidth + 100;
                    window.UpdateLayout();
                    Assert.That(outer.ScrollableWidth, Is.GreaterThan(50));
                }
            }
            finally {
                window.Close();
            }
        });
    }
}
