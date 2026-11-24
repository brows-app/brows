using Brows.Localization;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Brows.Agent;

[TestFixture]
[NonParallelizable]
internal sealed class AgentOperationsTest {
    private static T Wait<T>(Task<T> task, Dispatcher dispatcher) {
        var frame = new DispatcherFrame();
        var completed = dispatcher.InvokeAsync(async () => {
            try {
                return await task.WaitAsync(TimeSpan.FromSeconds(15));
            }
            finally {
                frame.Continue = false;
            }
        }).Task.Unwrap();
        Dispatcher.PushFrame(frame);
        return completed.GetAwaiter().GetResult();
    }

    private static Window ShowWindow(UIElement content, double left = -10000) {
        var window = new Window {
            Content = content,
            Height = 120,
            Left = left,
            ShowActivated = false,
            ShowInTaskbar = false,
            Title = "Test",
            Top = 0,
            Width = 320
        };
        window.Show();
        window.UpdateLayout();
        DispatcherTools.Pump(window.Dispatcher);
        return window;
    }

    private static ITranslation Translation() {
        var values = new Dictionary<string, string> {
            { "PressGesture_Delimiter", "+" },
            { "PressModifiers_Control", "Ctrl" }
        };
        var translation = new Mock<ITranslation>();
        translation
            .Setup(item => item.Value(It.IsAny<string>()))
            .Returns((string key) => values.TryGetValue(key, out var value) ? value : null);
        return translation.Object;
    }

    private static ILogEntry Entry(LogSeverity severity, string text) {
        var entry = new Mock<ILogEntry>();
        entry.Setup(item => item.LogDate).Returns(DateTime.UtcNow);
        entry.Setup(item => item.LogList).Returns(new[] { text });
        entry.Setup(item => item.LogName).Returns(nameof(AgentOperationsTest));
        entry.Setup(item => item.LogSeverity).Returns(severity);
        entry.Setup(item => item.LogType).Returns(typeof(AgentOperationsTest));
        return entry.Object;
    }

    private static AgentOperations Operations(Dispatcher dispatcher, AgentLog log, params Window[] windows) {
        return new AgentOperations(dispatcher, () => windows, Translation, log);
    }

    private static AgentResponse Handle(AgentOperations operations, AgentRequest request, Dispatcher dispatcher) {
        return Wait(operations.Handle(request, CancellationToken.None), dispatcher);
    }

    private static TextBox Focused(TextBox textBox) {
        textBox.Focus();
        DispatcherTools.Pump(textBox.Dispatcher);
        return textBox;
    }

    [Test]
    public void Handle_UnknownOp_Fails() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var response = Handle(Operations(dispatcher, log), new AgentRequest { Op = "nope" }, dispatcher);
            Assert.That(response.Ok, Is.False);
            Assert.That(response.Error, Does.Contain("\"nope\""));
        });
    }

    [Test]
    public void Handle_Type_SettlesWithTheTypedText() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var textBox = new TextBox();
            var window = ShowWindow(textBox);
            try {
                Focused(textBox);
                var operations = Operations(dispatcher, log, window);
                var request = new AgentRequest { Op = "type", Text = "abc", Quiet = 50, Timeout = 2000 };
                var response = Handle(operations, request, dispatcher);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(response.Ok, Is.True, response.Error);
                    Assert.That(response.Settled, Is.True);
                    Assert.That(response.Ui, Does.Contain("textbox [focus] \"abc\""));
                    Assert.That(response.Delivered, Is.EqualTo(3));
                    Assert.That(response.Requested, Is.EqualTo(3));
                    Assert.That(response.Window.Id, Is.EqualTo(AgentWindow.Id(window)));
                    Assert.That(response.Focus.Type, Is.EqualTo(nameof(TextBox)));
                    Assert.That(response.Palette, Is.EqualTo(new AgentPalette(false, false, null)));
                }
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Handle_WaitForMissingText_FailsAfterItsTimeout() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var window = ShowWindow(new TextBlock { Text = "present" });
            try {
                var operations = Operations(dispatcher, log, window);
                var request = new AgentRequest { Op = "wait", Text = "missing", Timeout = 300 };
                var elapsed = Stopwatch.StartNew();
                var response = Handle(operations, request, dispatcher);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(response.Ok, Is.False);
                    Assert.That(response.Error, Does.Contain("\"missing\""));
                    Assert.That(response.Ui, Does.Contain("\"present\""));
                    Assert.That(elapsed.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(250));
                }
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Handle_WaitForPresentText_Succeeds() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var window = ShowWindow(new TextBlock { Text = "present" });
            try {
                var operations = Operations(dispatcher, log, window);
                var request = new AgentRequest { Op = "wait", Text = "PRESENT", Timeout = 2000 };
                var response = Handle(operations, request, dispatcher);
                Assert.That(response.Ok, Is.True, response.Error);
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Handle_WindowIsNotOpen_Fails() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var window = ShowWindow(new TextBox());
            try {
                var operations = Operations(dispatcher, log, window);
                var response = Handle(operations, new AgentRequest { Op = "ui", Window = 12345 }, dispatcher);
                Assert.That(response.Ok, Is.False);
                Assert.That(response.Error, Does.Contain("12345"));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Handle_WindowId_TargetsThatWindowWhileAnotherIsActive() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var target = new TextBox();
            var other = new TextBox();
            var targetWindow = ShowWindow(target);
            var otherWindow = ShowWindow(other, left: -9500);
            try {
                Focused(target);
                Focused(other);
                var operations = Operations(dispatcher, log, targetWindow, otherWindow);
                var request = new AgentRequest {
                    Op = "type",
                    Text = "x",
                    Window = AgentWindow.Id(targetWindow),
                    Settle = false
                };
                var response = Handle(operations, request, dispatcher);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(response.Ok, Is.True, response.Error);
                    Assert.That(response.Window.Id, Is.EqualTo(AgentWindow.Id(targetWindow)));
                    Assert.That(response.Window.Index, Is.Zero);
                    Assert.That(target.Text, Is.EqualTo("x"));
                    Assert.That(other.Text, Is.Empty);
                }
            }
            finally {
                otherWindow.Close();
                targetWindow.Close();
            }
        });
    }

    [Test]
    public void Handle_PressWithAnInvalidSecondToken_FailsWithoutDeliveringAnyKey() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var textBox = new TextBox();
            var window = ShowWindow(textBox);
            try {
                Focused(textBox);
                var keys = 0;
                window.PreviewKeyDown += (s, e) => keys++;
                var operations = Operations(dispatcher, log, window);
                var request = new AgentRequest { Op = "press", Keys = "CTRL+L CTL+X" };
                var response = Handle(operations, request, dispatcher);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(response.Ok, Is.False);
                    Assert.That(response.Error, Does.Contain("\"CTL+X\""));
                    Assert.That(keys, Is.Zero);
                }
            }
            finally {
                window.Close();
            }
        });
    }

    private static Window ShowPalette(string text, out TextBox input) {
        return ShowPalette(text, out input, out _);
    }

    private static Window ShowPalette(string text, out TextBox input, out TextBox other) {
        var control = new CommandPaletteInputControl();
        other = new TextBox();
        var panel = new StackPanel();
        panel.Children.Add(control);
        panel.Children.Add(other);
        var window = new Window {
            Content = panel,
            Height = 180,
            Left = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            Top = 0,
            Width = 280
        };
        foreach (var (assembly, path) in new[] {
            ("Brows.Framework.Windows", "Resources/Themes/Light.FileExplorer"),
            ("Brows.Commander.Windows", "Resources/Styles/Brows.Style.CommandPaletteInput")
        }) {
            window.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri($"pack://application:,,,/{assembly};component/{path}.xaml")
            });
        }
        window.Show();
        window.UpdateLayout();
        input = (TextBox)control.Template.FindName("PART_Text", control);
        input.Text = text;
        Focused(input);
        return window;
    }

    [Test]
    public void Handle_PressWithExpectedPaletteTextThatChanged_FailsWithoutDeliveringAnyKey() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var window = ShowPalette("panel-remove", out _);
            try {
                var keys = 0;
                window.PreviewKeyDown += (s, e) => keys++;
                var operations = Operations(dispatcher, log, window);
                var request = new AgentRequest { Op = "press", Keys = "ENTER", Expect = "panel-add" };
                var response = Handle(operations, request, dispatcher);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(response.Ok, Is.False);
                    Assert.That(response.Error, Does.Contain("panel-remove"));
                    Assert.That(keys, Is.Zero);
                }
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Handle_PressWithExpectedPaletteText_DeliversTheKeys() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var window = ShowPalette("panel-add", out _);
            try {
                var keys = 0;
                window.PreviewKeyDown += (s, e) => keys++;
                var operations = Operations(dispatcher, log, window);
                var request = new AgentRequest { Op = "press", Keys = "END", Expect = "panel-add", Settle = false };
                var response = Handle(operations, request, dispatcher);
                Assert.That(response.Ok, Is.True, response.Error);
                Assert.That(keys, Is.EqualTo(1));
            }
            finally {
                window.Close();
            }
        });
    }

    [TestCase("type", "panel-add")]
    [TestCase("press", "CTRL+A")]
    public void Handle_RequiringAnUnfocusedPalette_FailsWithoutDeliveringAnything(string op, string value) {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var window = ShowPalette("", out _, out var other);
            try {
                Focused(other);
                var keys = 0;
                window.PreviewKeyDown += (s, e) => keys++;
                var operations = Operations(dispatcher, log, window);
                var request = new AgentRequest {
                    Op = op,
                    Text = op == "type" ? value : null,
                    Keys = op == "press" ? value : null,
                    RequirePalette = true
                };
                var response = Handle(operations, request, dispatcher);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(response.Ok, Is.False);
                    Assert.That(response.Error, Does.Contain("palette"));
                    Assert.That(other.Text, Is.Empty);
                    Assert.That(keys, Is.Zero);
                }
            }
            finally {
                window.Close();
            }
        });
    }

    [TestCase("type")]
    [TestCase("press")]
    public void Handle_RequiresPalette_RestoresItsFocusFromAnotherWindow(string op) {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var window = ShowPalette("panel-add", out var input);
            var other = new TextBox();
            var otherWindow = ShowWindow(other, left: -9600);
            try {
                Focused(input);
                Focused(other);
                Assert.That(other.IsKeyboardFocused, Is.True);
                Assert.That(FocusManager.GetFocusedElement(window), Is.SameAs(input));
                var keys = 0;
                window.PreviewKeyDown += (s, e) => keys++;
                var id = AgentWindow.Id(window);
                var request = op == "type"
                    ? new AgentRequest { Op = op, Window = id, Text = "x", RequirePalette = true, Settle = false }
                    : new AgentRequest {
                        Op = op,
                        Window = id,
                        Keys = "END",
                        Expect = "panel-add",
                        RequirePalette = true,
                        Settle = false
                    };
                input.CaretIndex = input.Text.Length;
                var response = Handle(Operations(dispatcher, log, window, otherWindow), request, dispatcher);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(response.Ok, Is.True, response.Error);
                    Assert.That(response.Delivered, Is.EqualTo(1));
                    Assert.That(input.Text, Is.EqualTo(op == "type" ? "panel-addx" : "panel-add"));
                    Assert.That(keys, Is.EqualTo(op == "press" ? 1 : 0));
                    Assert.That(other.Text, Is.Empty);
                }
            }
            finally {
                otherWindow.Close();
                window.Close();
            }
        });
    }

    [Test]
    public void Handle_TypeRequiringThePalette_StopsWhenThePaletteLosesFocus() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var window = ShowPalette("", out var input, out var other);
            try {
                input.TextChanged += (s, e) => {
                    if (input.Text.Length == 2) {
                        other.Focus();
                    }
                };
                var operations = Operations(dispatcher, log, window);
                var request = new AgentRequest { Op = "type", Text = "panel-add", RequirePalette = true };
                var response = Handle(operations, request, dispatcher);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(response.Ok, Is.False);
                    Assert.That(response.Delivered, Is.EqualTo(2));
                    Assert.That(input.Text, Is.EqualTo("pa"));
                    Assert.That(other.Text, Is.Empty);
                }
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Handle_TypeRequiringThePalette_TypesIntoTheFocusedPalette() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var window = ShowPalette("", out var input);
            try {
                var operations = Operations(dispatcher, log, window);
                var request = new AgentRequest {
                    Op = "type",
                    Text = "panel-add",
                    RequirePalette = true,
                    Settle = false
                };
                var response = Handle(operations, request, dispatcher);
                Assert.That(response.Ok, Is.True, response.Error);
                Assert.That(input.Text, Is.EqualTo("panel-add"));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Handle_TypeTimesOut_ReportsPartialDeliveryAndStops() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var textBox = new TextBox();
            textBox.TextChanged += (s, e) => Thread.Sleep(5);
            var window = ShowWindow(textBox);
            try {
                Focused(textBox);
                var operations = Operations(dispatcher, log, window);
                var request = new AgentRequest { Op = "type", Text = new string('x', 1000), Timeout = 200 };
                var response = Handle(operations, request, dispatcher);
                var typed = textBox.Text.Length;
                for (var i = 0; i < 10; i++) {
                    DispatcherTools.Pump(dispatcher);
                }
                using (Assert.EnterMultipleScope()) {
                    Assert.That(response.Ok, Is.False);
                    Assert.That(response.Error, Does.Contain("timed out"));
                    Assert.That(response.Requested, Is.EqualTo(1000));
                    Assert.That(response.Delivered, Is.GreaterThan(0).And.LessThan(1000));
                    Assert.That(textBox.Text.Length, Is.EqualTo(response.Delivered));
                    Assert.That(textBox.Text.Length, Is.EqualTo(typed));
                }
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Handle_ReportsEachErrorOnce() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var operations = Operations(dispatcher, log);
            log.Receive(Entry(LogSeverity.Info, "info"));
            log.Receive(Entry(LogSeverity.Error, "broken"));
            var first = Handle(operations, new AgentRequest { Op = "windows" }, dispatcher);
            var second = Handle(operations, new AgentRequest { Op = "windows" }, dispatcher);
            Assert.That(first.Errors, Has.Count.EqualTo(1));
            Assert.That(first.Errors[0], Does.Contain("broken"));
            Assert.That(second.Errors, Is.Empty);
        });
    }

    [Test]
    public void Handle_Windows_DescribesEachWindow() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var first = ShowWindow(new TextBox());
            var second = ShowWindow(new TextBox(), left: -9500);
            try {
                var operations = Operations(dispatcher, log, first, second);
                var request = new AgentRequest { Op = "windows", Window = AgentWindow.Id(second) };
                var response = Handle(operations, request, dispatcher);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(response.Ok, Is.True, response.Error);
                    Assert.That(response.Windows, Has.Count.EqualTo(2));
                    Assert.That(response.Windows[0].Id, Is.EqualTo(AgentWindow.Id(first)));
                    Assert.That(response.Windows[1].Index, Is.EqualTo(1));
                    Assert.That(response.Window.Id, Is.EqualTo(AgentWindow.Id(second)));
                }
            }
            finally {
                second.Close();
                first.Close();
            }
        });
    }

    [Test]
    public void Handle_WithoutWindows_ReportsNoWindowAndFailsInput() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var operations = Operations(dispatcher, log);
            var windows = Handle(operations, new AgentRequest { Op = "windows" }, dispatcher);
            var typed = Handle(operations, new AgentRequest { Op = "type", Text = "x" }, dispatcher);
            using (Assert.EnterMultipleScope()) {
                Assert.That(windows.Ok, Is.True);
                Assert.That(windows.Windows, Is.Empty);
                Assert.That(windows.Window, Is.Null);
                Assert.That(typed.Ok, Is.False);
                Assert.That(typed.Error, Does.Contain("no open window"));
            }
        });
    }

    [Test]
    public void Handle_WindowsWithRequestedIdAndNoWindows_Fails() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var operations = Operations(dispatcher, log);
            var request = new AgentRequest { Op = "windows", Window = 12345 };
            var response = Handle(operations, request, dispatcher);
            using (Assert.EnterMultipleScope()) {
                Assert.That(response.Ok, Is.False);
                Assert.That(response.Error, Is.EqualTo("Window 12345 is not open."));
            }
        });
    }

    [Test]
    public void Handle_Log_ReturnsEntriesSince() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            log.Receive(Entry(LogSeverity.Info, "a"));
            log.Receive(Entry(LogSeverity.Info, "b"));
            var response = Handle(Operations(dispatcher, log), new AgentRequest { Op = "log", Since = 1 }, dispatcher);
            Assert.That(response.Log, Has.Count.EqualTo(1));
            Assert.That(response.Log[0].Text, Is.EqualTo("b"));
        });
    }

    [Test]
    public void Handle_NonPositiveTimeout_Fails() {
        DispatcherTools.Run(dispatcher => {
            using var log = new AgentLog();
            var response = Handle(Operations(dispatcher, log), new AgentRequest { Op = "ui", Timeout = 0 }, dispatcher);
            Assert.That(response.Ok, Is.False);
            Assert.That(response.Error, Does.Contain("timeout"));
        });
    }
}
