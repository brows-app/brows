using Brows.Panels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Brows;

[TestFixture]
[NonParallelizable]
internal sealed class PanelStateControlTest {
    [Test]
    public void SortingCount_UsesColumnAfterSeparator() {
        DispatcherTools.Run(_ => {
            var window = new Window {
                Height = 160,
                Left = -10000,
                ShowActivated = false,
                ShowInTaskbar = false,
                Width = 480
            };
            window.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri(
                    "/Brows.Framework.Windows;component/Resources/Styles.xaml",
                    UriKind.Relative)
            });
            window.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri(
                    "/Brows.Commander.Windows;component/Resources/Styles.xaml",
                    UriKind.Relative)
            });
            var control = new PanelStateControl {
                ViewModel = new PanelState {
                    ItemsSortingCount = 1
                }
            };
            try {
                window.Content = control;
                window.Show();
                window.UpdateLayout();
                control.ApplyTemplate();

                var sortingLabel = (Label)control.Template.FindName("PanelState_ItemsSortingCount", control);
                Assert.Multiple(() => {
                    Assert.That(sortingLabel.Content, Is.EqualTo(1));
                    Assert.That(Grid.GetColumn(sortingLabel), Is.EqualTo(3));
                });
            }
            finally {
                window.Close();
            }
        });
    }
}
