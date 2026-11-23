using Brows.Operations;
using Brows.Windows.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Brows.Resource;

[TestFixture]
internal sealed class OperatorControlStyleTest {
    private const string OperationName = @"Copy files to D:\Backups\Projects\2026\brows-private-archive";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static ResourceDictionary Load(string assembly, string name) {
        return new ResourceDictionary {
            Source = new Uri($"pack://application:,,,/{assembly};component/{name}.xaml")
        };
    }

    private static IOperator CreateOperator() {
        /*
         * The package's factory is internal and is normally discovered through composition,
         * so create it the same way the composition container does.
         */
        var factoryType = typeof(IOperatorFactory).Assembly.GetType(
            "Brows.Operations.OperatorFactory",
            throwOnError: true);
        var factory = (IOperatorFactory)Activator.CreateInstance(factoryType, nonPublic: true);
        return factory.Create();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) {
                yield return match;
            }
            foreach (var descendant in Descendants<T>(child)) {
                yield return descendant;
            }
        }
    }

    private static bool PumpUntil(Dispatcher dispatcher, Window window, Func<bool> condition) {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline) {
            DispatcherTools.Pump(dispatcher);
            window.UpdateLayout();
            if (condition()) {
                return true;
            }
        }
        return false;
    }

    private static Button CancelButton(OperatorControl control) {
        return Descendants<Button>(control).SingleOrDefault(button => Equals(button.Content, "Cancel"));
    }

    private static Window CreateWindow(double width, out OperatorControl control) {
        var window = new Window {
            Height = 400,
            Left = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            Width = width,
            WindowStyle = WindowStyle.None
        };
        window.Resources.MergedDictionaries.Add(Load("Brows.Framework.Windows", "Resources/Themes/Dark.FileExplorer"));
        window.Resources.MergedDictionaries.Add(Load("Brows.Framework.Windows", "Resources/Fonts"));
        window.Resources.MergedDictionaries.Add(
            Load("Brows.Commander.Windows", "Resources/Styles/Brows.Style.OperatorControl"));
        control = new OperatorControl {
            Style = (Style)window.FindResource("Brows.Style.OperatorControl")
        };
        window.Content = control;
        return window;
    }

    [Test]
    public void Style_UsesResponsiveDisplay() {
        DispatcherTools.Run(dispatcher => {
            var window = CreateWindow(400, out var control);
            try {
                Assert.That(control.OperationDisplay, Is.EqualTo(OperationDisplay.Responsive));
            }
            finally {
                window.Close();
            }
        });
    }

    [TestCase(300)]
    [TestCase(380)]
    [TestCase(600)]
    public void NarrowWidth_KeepsNameAndCancelCommandInView(double width) {
        DispatcherTools.Run(dispatcher => {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = CreateWindow(width, out var control);
            try {
                var @operator = CreateOperator();
                @operator.Operate(OperationName, async (progress, token) => {
                    progress.Change(
                        setProgress: 412_000_000L,
                        setTarget: 1_288_490_188L,
                        kind: OperationProgressKind.FileSize,
                        data: @"C:\Users\person\Documents\Projects\brows\sln\export\source\Big file.iso");
                    await release.Task.WaitAsync(token);
                });
                control.Operator = @operator;
                window.Show();

                var cancelShown = PumpUntil(dispatcher, window, () => CancelButton(control) is { ActualWidth: > 0 });
                Assert.That(cancelShown, Is.True, "The Cancel command was not shown.");
                DispatcherTools.Pump(dispatcher);
                window.UpdateLayout();

                var cancel = CancelButton(control);
                var name = Descendants<TextBlock>(control).Single(text => text.Text == OperationName);
                var cancelLeft = cancel.TranslatePoint(new Point(), control).X;
                var cancelCenter = cancel.TranslatePoint(
                    new Point(cancel.ActualWidth / 2, cancel.ActualHeight / 2),
                    control);
                var nameLeft = name.TranslatePoint(new Point(), control).X;
                var nameRight = name.TranslatePoint(new Point(name.ActualWidth, 0), control).X;
                /*
                 * Probe the middle of the part of the name left of Cancel, so that a name that is hidden,
                 * collapsed, or covered there does not pass on its layout position alone.
                 */
                var nameProbe = new Point(
                    (nameLeft + Math.Min(nameRight, cancelLeft)) / 2,
                    name.TranslatePoint(new Point(0, name.ActualHeight / 2), control).Y);
                var hit = VisualTreeHelper.HitTest(control, cancelCenter)?.VisualHit;
                var cancelIsHit = hit is not null && (ReferenceEquals(hit, cancel) || cancel.IsAncestorOf(hit));
                var nameHit = control.InputHitTest(nameProbe) as DependencyObject;
                var nameIsHit = nameHit is not null && (ReferenceEquals(nameHit, name) || name.IsAncestorOf(nameHit));
                using (Assert.EnterMultipleScope()) {
                    Assert.That(cancelLeft, Is.GreaterThanOrEqualTo(0), "Cancel left");
                    Assert.That(cancelLeft + cancel.ActualWidth, Is.LessThanOrEqualTo(control.ActualWidth + 1),
                                "Cancel right");
                    Assert.That(cancelIsHit, Is.True, "Cancel hit test");
                    Assert.That(name.IsVisible, Is.True, "Name visible");
                    Assert.That(name.ActualWidth, Is.GreaterThan(0), "Name width");
                    Assert.That(nameLeft, Is.GreaterThanOrEqualTo(0), "Name left");
                    Assert.That(nameRight, Is.LessThanOrEqualTo(cancelLeft + 1), "Name right");
                    Assert.That(nameIsHit, Is.True, "Name hit test");
                }
            }
            finally {
                release.TrySetResult();
                window.Close();
            }
        });
    }
}
