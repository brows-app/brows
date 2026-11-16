using Brows.Panels;
using Moq;
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Brows;

[TestFixture]
[NonParallelizable]
internal sealed class PanelControlTest {
    private static DragEventArgs DropArgs(PanelControl control) {
        var constructor = typeof(DragEventArgs).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(IDataObject), typeof(DragDropKeyStates), typeof(DragDropEffects),
                typeof(DependencyObject), typeof(Point)],
            modifiers: null) ?? throw new InvalidOperationException("The WPF drag-event constructor was not found.");
        var args = (DragEventArgs)constructor.Invoke([
            new DataObject(),
            DragDropKeyStates.None,
            DragDropEffects.None,
            control,
            new Point()
        ]);
        args.RoutedEvent = DragDrop.DropEvent;
        return args;
    }

    private static PanelControl NewControl(IPanel panel) {
        var control = new PanelControl {
            ViewModel = panel
        };
        control.DataContext = control;
        return control;
    }

    [Test]
    public void Drop_UsesPanelViewModelAndClearsTransientState() {
        DispatcherTools.Run(_ => {
            var panel = Mock.Of<IPanel>();
            var control = NewControl(panel);
            var controller = (IPanelController)control;
            IPanelDrop received = null;
            controller.Drop += (_, _) => received = controller.Dropped;

            control.RaiseEvent(DropArgs(control));

            Assert.Multiple(() => {
                Assert.That(received, Is.Not.Null);
                Assert.That(received.Target, Is.SameAs(panel));
                Assert.That(controller.Dropped, Is.Null);
            });
        });
    }

    [Test]
    public void Drop_WhenSubscriberThrowsClearsTransientState() {
        DispatcherTools.Run(_ => {
            var control = NewControl(Mock.Of<IPanel>());
            var controller = (IPanelController)control;
            EventHandler handler = (_, _) => throw new InvalidOperationException();
            controller.Drop += handler;

            Assert.Throws<InvalidOperationException>(() => control.RaiseEvent(DropArgs(control)));

            controller.Drop -= handler;
            Assert.That(controller.Dropped, Is.Null);
        });
    }

    [Test]
    public void Padding_TemplateReservesPanelContentSpace() {
        DispatcherTools.Run(_ => {
            var window = new Window {
                Height = 160,
                Left = -10000,
                ShowActivated = false,
                ShowInTaskbar = false,
                Width = 320
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
            var control = NewControl(Mock.Of<IPanel>());
            var padding = new Thickness(5, 0, 0, 0);
            control.Padding = padding;
            try {
                window.Content = control;
                window.Show();
                window.UpdateLayout();
                control.ApplyTemplate();

                var border = (Border)control.Template.FindName("PanelControl_Border", control);
                Assert.That(border.Padding, Is.EqualTo(padding));
            }
            finally {
                window.Close();
            }
        });
    }
}
