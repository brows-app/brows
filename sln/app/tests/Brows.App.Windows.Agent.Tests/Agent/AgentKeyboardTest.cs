using Brows.Localization;
using Brows.Triggers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Brows.Agent;

[TestFixture]
[NonParallelizable]
internal sealed class AgentKeyboardTest {
    private const int VK_SHIFT = 0x10;
    private const int VK_RSHIFT = 0xA1;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState([Out] byte[] lpKeyState);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKeyboardState([In] byte[] lpKeyState);

    private static void Wait(Task task, Dispatcher dispatcher) {
        var frame = new DispatcherFrame();
        var completed = dispatcher.InvokeAsync(async () => {
            try {
                await task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally {
                frame.Continue = false;
            }
        }).Task.Unwrap();
        Dispatcher.PushFrame(frame);
        completed.GetAwaiter().GetResult();
    }

    private static Window ShowWindow(UIElement content, double left = -10000) {
        var window = new Window {
            Content = content,
            Height = 120,
            Left = left,
            ShowActivated = false,
            ShowInTaskbar = false,
            Top = 0,
            Width = 320
        };
        window.Show();
        window.UpdateLayout();
        DispatcherTools.Pump(window.Dispatcher);
        return window;
    }

    private static TextBox Focused(TextBox textBox) {
        textBox.Focus();
        textBox.CaretIndex = textBox.Text.Length;
        DispatcherTools.Pump(textBox.Dispatcher);
        return textBox;
    }

    private static ITranslation Translation() {
        var values = new Dictionary<string, string> {
            { "PressGesture_Delimiter", "+" },
            { "PressKey_Escape", "Esc" },
            { "PressModifiers_Control", "Ctrl" },
            { "PressModifiers_Windows", "Win" }
        };
        var translation = new Mock<ITranslation>();
        translation
            .Setup(item => item.Value(It.IsAny<string>()))
            .Returns((string key) => values.TryGetValue(key, out var value) ? value : null);
        return translation.Object;
    }

    private static IReadOnlyList<PressGesture> Keys(string keys) {
        return AgentKeyboard.Parse(keys, Translation());
    }

    [Test]
    public void Type_DeliversTextToTheFocusedTextBox() {
        DispatcherTools.Run(dispatcher => {
            var textBox = new TextBox();
            var window = ShowWindow(textBox);
            try {
                Focused(textBox);
                var previews = new List<string>();
                var delivered = new List<int>();
                window.PreviewTextInput += (s, e) => previews.Add(e.Text);
                Wait(AgentKeyboard.Type(window, "abc", delivered.Add, null, CancellationToken.None), dispatcher);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(textBox.Text, Is.EqualTo("abc"));
                    Assert.That(previews, Is.EqualTo(new[] { "a", "b", "c" }));
                    Assert.That(delivered, Is.EqualTo(new[] { 1, 2, 3 }));
                }
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Type_KeepsSurrogatePairsWhole() {
        DispatcherTools.Run(dispatcher => {
            var textBox = new TextBox();
            var window = ShowWindow(textBox);
            try {
                Focused(textBox);
                var delivered = new List<int>();
                Wait(AgentKeyboard.Type(window, "\U0001D11E", delivered.Add, null, CancellationToken.None), dispatcher);
                Assert.That(textBox.Text, Is.EqualTo("\U0001D11E"));
                Assert.That(delivered, Is.EqualTo(new[] { 1 }));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Type_ControlCharacter_ThrowsBeforeDeliveringAnything() {
        DispatcherTools.Run(dispatcher => {
            var textBox = new TextBox();
            var window = ShowWindow(textBox);
            try {
                Focused(textBox);
                var typed = AgentKeyboard.Type(window, "a\nb", null, null, CancellationToken.None);
                Assert.That(() => Wait(typed, dispatcher), Throws.ArgumentException.With.Message.Contains("U+000A"));
                Assert.That(textBox.Text, Is.Empty);
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Type_Cancelled_StopsDelivery() {
        DispatcherTools.Run(dispatcher => {
            var textBox = new TextBox();
            var window = ShowWindow(textBox);
            try {
                Focused(textBox);
                using var cancellation = new CancellationTokenSource();
                var delivered = 0;
                void deliver(int count) {
                    delivered = count;
                    if (count == 2) {
                        cancellation.Cancel();
                    }
                }
                var typed = AgentKeyboard.Type(window, "abcdef", deliver, null, cancellation.Token);
                Assert.That(() => Wait(typed, dispatcher), Throws.InstanceOf<OperationCanceledException>());
                DispatcherTools.Pump(dispatcher);
                Assert.That(textBox.Text, Is.EqualTo("ab"));
                Assert.That(delivered, Is.EqualTo(2));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Press_SetsModifiersOnlyWhileTheKeyIsProcessed() {
        DispatcherTools.Run(dispatcher => {
            var textBox = new TextBox();
            var window = ShowWindow(textBox);
            try {
                Focused(textBox);
                var previews = new List<(Key Key, ModifierKeys Modifiers)>();
                var keyDowns = new List<Key>();
                window.PreviewKeyDown += (s, e) => previews.Add((e.Key, e.KeyboardDevice.Modifiers));
                window.KeyDown += (s, e) => keyDowns.Add(e.Key);
                Wait(AgentKeyboard.Press(window, Keys("CTRL+L"), null, null, CancellationToken.None), dispatcher);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(previews, Is.EqualTo(new[] { (Key.L, ModifierKeys.Control) }));
                    Assert.That(keyDowns, Is.EqualTo(new[] { Key.L }));
                    Assert.That(Keyboard.Modifiers, Is.EqualTo(ModifierKeys.None));
                }
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Press_EditsTheFocusedTextBox() {
        DispatcherTools.Run(dispatcher => {
            var textBox = new TextBox { Text = "abc" };
            var window = ShowWindow(textBox);
            try {
                Focused(textBox);
                var pressed = AgentKeyboard.Press(window, Keys("BACK SHIFT+LEFT"), null, null, CancellationToken.None);
                Wait(pressed, dispatcher);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(textBox.Text, Is.EqualTo("ab"));
                    Assert.That(textBox.SelectionLength, Is.EqualTo(1));
                }
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Press_ReachesBrowsGestureHandlers() {
        DispatcherTools.Run(dispatcher => {
            var textBox = new TextBox();
            var window = ShowWindow(textBox);
            try {
                Focused(textBox);
                var gestures = new List<IGesture>();
                textBox.PreviewKeyDown += (s, e) => e.Trigger(args => gestures.Add(args.Gesture));
                Wait(AgentKeyboard.Press(window, Keys("CTRL+L"), null, null, CancellationToken.None), dispatcher);
                Assert.That(gestures, Is.EqualTo(new IGesture[] {
                    new PressGesture(PressKey.L, PressModifiers.Control)
                }));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Press_IgnoresModifiersAlreadyInTheKeyboardState() {
        DispatcherTools.Run(dispatcher => {
            var textBox = new TextBox();
            var window = ShowWindow(textBox);
            var original = new byte[256];
            Assert.That(GetKeyboardState(original), Is.True);
            try {
                Focused(textBox);
                var shifted = (byte[])original.Clone();
                shifted[VK_SHIFT] |= 0x80;
                shifted[VK_RSHIFT] |= 0x80;
                Assert.That(SetKeyboardState(shifted), Is.True);
                var modifiers = new List<ModifierKeys>();
                window.PreviewKeyDown += (s, e) => modifiers.Add(e.KeyboardDevice.Modifiers);
                Wait(AgentKeyboard.Press(window, Keys("CTRL+L"), null, null, CancellationToken.None), dispatcher);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(modifiers, Is.EqualTo(new[] { ModifierKeys.Control }));
                    Assert.That(Keyboard.IsKeyDown(Key.RightShift), Is.True);
                }
            }
            finally {
                SetKeyboardState(original);
                window.Close();
            }
        });
    }

    [Test]
    public void Input_AfterAnotherWindowTookActivation_ReachesTheTargetWindow() {
        DispatcherTools.Run(dispatcher => {
            var target = new TextBox { Text = "aaaa" };
            var other = new TextBox { Text = "bbbb" };
            var targetWindow = ShowWindow(target);
            var otherWindow = ShowWindow(other, left: -9500);
            try {
                Focused(target);
                Focused(other);
                Assume.That(Keyboard.FocusedElement, Is.SameAs(other));
                Wait(AgentKeyboard.Press(targetWindow, Keys("BACK"), null, null, CancellationToken.None), dispatcher);
                Wait(AgentKeyboard.Type(targetWindow, "x", null, null, CancellationToken.None), dispatcher);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(target.Text, Is.EqualTo("aaax"));
                    Assert.That(other.Text, Is.EqualTo("bbbb"));
                }
            }
            finally {
                otherWindow.Close();
                targetWindow.Close();
            }
        });
    }

    [Test]
    public void Input_WhenFocusCannotMoveIntoTheWindow_ThrowsAndDeliversNothing() {
        DispatcherTools.Run(dispatcher => {
            var other = new TextBox { Text = "bbbb" };
            var targetWindow = ShowWindow(new TextBlock { Text = "nothing to focus" });
            targetWindow.Focusable = false;
            var otherWindow = ShowWindow(other, left: -9500);
            try {
                Focused(other);
                Assume.That(Keyboard.FocusedElement, Is.SameAs(other));
                var pressed = AgentKeyboard.Press(targetWindow, Keys("BACK"), null, null, CancellationToken.None);
                Assert.That(() => Wait(pressed, dispatcher), Throws.InstanceOf<AgentFocusException>());
                var typed = AgentKeyboard.Type(targetWindow, "x", null, null, CancellationToken.None);
                Assert.That(() => Wait(typed, dispatcher), Throws.InstanceOf<AgentFocusException>());
                Assert.That(other.Text, Is.EqualTo("bbbb"));
            }
            finally {
                otherWindow.Close();
                targetWindow.Close();
            }
        });
    }

    [Test]
    public void Parse_ReturnsEachGesture() {
        var gestures = Keys("CTRL+L  ESC");
        Assert.That(gestures, Is.EqualTo(new[] {
            new PressGesture(PressKey.L, PressModifiers.Control),
            new PressGesture(PressKey.Escape)
        }));
    }

    [TestCase("CTL+L", "\"CTL+L\"")]
    [TestCase("CTRL+L CTL+X", "\"CTL+X\"")]
    [TestCase("WIN+L", "\"WIN+L\"")]
    [TestCase("CTRL", "\"CTRL\"")]
    public void Parse_InvalidToken_ThrowsNamingTheToken(string keys, string token) {
        Assert.That(() => Keys(keys), Throws.InstanceOf<FormatException>().With.Message.Contains(token));
    }

    [TestCase(null)]
    [TestCase(" ")]
    public void Parse_NoGestures_Throws(string keys) {
        Assert.That(() => Keys(keys), Throws.InstanceOf<FormatException>());
    }
}
