using Brows.Commands;
using Moq;
using System;
using System.Windows;
using System.Windows.Media;

namespace Brows;

[TestFixture]
[NonParallelizable]
internal sealed class CommanderWindowTest {
    private static ResourceDictionary LoadStyle() {
        return new ResourceDictionary {
            Source = new Uri(
                "/Brows.Commander.Windows;component/Resources/Styles/Brows.Style.CommanderWindow.xaml",
                UriKind.Relative)
        };
    }

    private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject {
        if (parent is T match) {
            return match;
        }
        var children = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < children; i++) {
            var child = FindVisualChild<T>(VisualTreeHelper.GetChild(parent, i));
            if (child is not null) {
                return child;
            }
        }
        return null;
    }

    [Test]
    public void ContentTemplate_CreatesAnIndependentCommanderControlForEachWindow() {
        DispatcherTools.Run(_ => {
            var firstCommander = Mock.Of<ICommander>();
            var secondCommander = Mock.Of<ICommander>();
            var firstWindow = new CommanderWindow {
                Commander = firstCommander,
                Height = 160,
                Left = -10000,
                ShowActivated = false,
                ShowInTaskbar = false,
                Width = 320
            };
            var secondWindow = new CommanderWindow {
                Commander = secondCommander,
                Height = 160,
                Left = -10000,
                ShowActivated = false,
                ShowInTaskbar = false,
                Width = 320
            };
            var style = LoadStyle();
            firstWindow.Resources.MergedDictionaries.Add(style);
            secondWindow.Resources.MergedDictionaries.Add(style);
            try {
                firstWindow.Show();
                secondWindow.Show();
                firstWindow.UpdateLayout();
                secondWindow.UpdateLayout();

                var firstControl = FindVisualChild<CommanderControl>(firstWindow);
                var secondControl = FindVisualChild<CommanderControl>(secondWindow);
                Assert.That(firstControl, Is.Not.Null);
                Assert.That(secondControl, Is.Not.Null);
                Assert.That(secondControl, Is.Not.SameAs(firstControl));
                Assert.That(firstControl.ViewModel, Is.SameAs(firstCommander));
                Assert.That(secondControl.ViewModel, Is.SameAs(secondCommander));
            }
            finally {
                secondWindow.Close();
                firstWindow.Close();
            }
        });
    }
}
