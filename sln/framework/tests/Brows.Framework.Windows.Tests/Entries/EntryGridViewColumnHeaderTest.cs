using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryGridViewColumnHeaderTest {
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

    [TestCase("Light")]
    [TestCase("Dark")]
    public void Content_UsesSegoeUIWhenEntryTextUsesConsolas(string mode) {
        DispatcherTools.Run(dispatcher => {
            var window = new Window { ShowInTaskbar = false, ShowActivated = false, Left = -10000 };
            try {
                window.Resources.MergedDictionaries.Add(new ResourceDictionary {
                    Source = new Uri(
                        $"pack://application:,,,/Brows.Framework.Windows;component/Resources/Themes/" +
                        $"{mode}.FileExplorer.xaml")
                });
                window.Resources.MergedDictionaries.Add(new ResourceDictionary {
                    Source = new Uri(
                        "pack://application:,,,/Brows.Framework.Windows;component/Resources/Resources.xaml")
                });
                var header = new EntryGridViewColumnHeader { Display = "Name" };
                var view = new GridView {
                    ColumnHeaderContainerStyle = (Style)window.FindResource("Brows.Style.EntryGridViewColumnHeader")
                };
                view.Columns.Add(new GridViewColumn {
                    Header = header,
                    Width = 200,
                    DisplayMemberBinding = new Binding(".")
                });
                var list = new ListView {
                    FontFamily = new FontFamily("Consolas"),
                    View = view
                };
                list.Items.Add("Entry");
                window.Content = list;
                window.Show();
                window.UpdateLayout();

                var text = FindText(header, "Name");
                Assert.That(text, Is.Not.Null);
                Assert.That(text.FontFamily.Source, Is.EqualTo("Segoe UI"));
                var entryText = FindText(list, "Entry");
                Assert.That(entryText, Is.Not.Null);
                Assert.That(entryText.FontFamily.Source, Is.EqualTo("Consolas"));
            }
            finally {
                window.Close();
            }
        });
    }
}