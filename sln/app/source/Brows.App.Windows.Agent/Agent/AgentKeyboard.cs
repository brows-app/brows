using Brows.Localization;
using Brows.Triggers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Brows.Agent;

/*
 * Delivers keyboard input to a window through WPF's input manager, so the window's handlers see it as they
 * see input from the keyboard, without the window being the foreground window. Modifier keys are set in the
 * keyboard state of the window's thread only while a key is processed, which affects no other thread.
 *
 * Input is delivered only while keyboard focus is in the window. WPF routes a key-down that a preview handler
 * did not handle to the element with keyboard focus, whatever the source of the preview, so a key sent while
 * focus is elsewhere would act on another window.
 */
internal static class AgentKeyboard {
    private const byte StateDown = 0x80;
    private const byte StateNotDown = 0x7F;
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;

    private static readonly int[] ModifierVirtualKeys = [
        VK_SHIFT, VK_LSHIFT, VK_RSHIFT,
        VK_CONTROL, VK_LCONTROL, VK_RCONTROL,
        VK_MENU, VK_LMENU, VK_RMENU,
        VK_LWIN, VK_RWIN
    ];

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState([Out] byte[] lpKeyState);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKeyboardState([In] byte[] lpKeyState);

    private static IEnumerable<int> RequestedVirtualKeys(ModifierKeys modifiers) {
        if (modifiers.HasFlag(ModifierKeys.Alt)) {
            yield return VK_MENU;
            yield return VK_LMENU;
        }
        if (modifiers.HasFlag(ModifierKeys.Control)) {
            yield return VK_CONTROL;
            yield return VK_LCONTROL;
        }
        if (modifiers.HasFlag(ModifierKeys.Shift)) {
            yield return VK_SHIFT;
            yield return VK_LSHIFT;
        }
    }

    private static byte[] GetState() {
        var state = new byte[256];
        if (!GetKeyboardState(state)) {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        return state;
    }

    private static void SetState(byte[] state) {
        if (!SetKeyboardState(state)) {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    private static void Send(Window window, Key key, RoutedEvent routedEvent, Action check = null) {
        var source = PresentationSource.FromVisual(window);
        if (source is null) {
            throw new InvalidOperationException("The window is not shown.");
        }
        var target = ResolveTarget(window);
        check?.Invoke();
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) {
            RoutedEvent = routedEvent,
            Source = target
        };
        InputManager.Current.ProcessInput(args);
    }

    private static void Press(Window window, PressGesture gesture, Action check) {
        var key = (Key)gesture.Key;
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        var saved = GetState();
        var state = (byte[])saved.Clone();
        /*
         * Build the exact modifier state of the gesture. A modifier left down in the thread's state would
         * otherwise turn the requested gesture into another one.
         */
        foreach (var modifierKey in ModifierVirtualKeys) {
            state[modifierKey] &= StateNotDown;
        }
        foreach (var modifierKey in RequestedVirtualKeys((ModifierKeys)gesture.Modifiers)) {
            state[modifierKey] |= StateDown;
        }
        try {
            state[virtualKey] |= StateDown;
            SetState(state);
            Send(window, key, Keyboard.PreviewKeyDownEvent, check);
            state[virtualKey] &= StateNotDown;
            SetState(state);
            Send(window, key, Keyboard.PreviewKeyUpEvent);
        }
        finally {
            SetState(saved);
        }
    }

    private static Task Idle(Dispatcher dispatcher, CancellationToken token) {
        return dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle, token).Task;
    }

    internal static IReadOnlyList<string> TextElements(string text) {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var c in text) {
            if (char.IsControl(c)) {
                throw new ArgumentException(
                    $"The text contains the control character U+{(int)c:X4}. " +
                    "Use press for keys such as ENTER and TAB.");
            }
        }
        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext()) {
            elements.Add(enumerator.GetTextElement());
        }
        return elements;
    }

    internal static IInputElement ResolveTarget(Window window) {
        ArgumentNullException.ThrowIfNull(window);
        var focused = AgentWindow.KeyboardFocus(window);
        if (focused is not null) {
            return focused;
        }
        /*
         * Move keyboard focus to the window's logically focused element, as activating the window would.
         */
        Keyboard.Focus(FocusManager.GetFocusedElement(window) ?? window);
        focused = AgentWindow.KeyboardFocus(window);
        if (focused is not null) {
            return focused;
        }
        throw new AgentFocusException(AgentWindow.Id(window));
    }

    internal static IReadOnlyList<PressGesture> Parse(string keys, ITranslation translation) {
        ArgumentNullException.ThrowIfNull(translation);
        var tokens = (keys ?? "").Split(default(char[]), StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) {
            throw new FormatException("Specify one or more key gestures, such as CTRL+L or ENTER.");
        }
        var gestures = new List<PressGesture>();
        foreach (var token in tokens) {
            var gesture = default(PressGesture);
            try {
                gesture = (PressGesture)GestureExtension.Parse(new PressGesture(), token, translation);
            }
            catch (FormatException exception) {
                throw new FormatException($"\"{token}\" is not a key gesture.", exception);
            }
            if (gesture.Key == PressKey.None) {
                throw new FormatException($"\"{token}\" does not name a key.");
            }
            if (gesture.Modifiers.HasFlag(PressModifiers.Windows)) {
                throw new FormatException($"\"{token}\": Brows does not read the Windows key as a modifier.");
            }
            gestures.Add(gesture);
        }
        return gestures;
    }

    /*
     * The check runs after restoring focus, in the same dispatcher turn as delivery.
     */
    internal static async Task Type(Window window, string text, Action<int> delivered, Action check,
                                    CancellationToken token) {
        ArgumentNullException.ThrowIfNull(window);
        var elements = TextElements(text);
        var count = 0;
        foreach (var element in elements) {
            token.ThrowIfCancellationRequested();
            var target = ResolveTarget(window);
            check?.Invoke();
            TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, target, element));
            delivered?.Invoke(++count);
            await Idle(window.Dispatcher, token);
        }
    }

    internal static async Task Press(Window window, IReadOnlyList<PressGesture> gestures, Action<int> delivered,
                                     Action check, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(gestures);
        var count = 0;
        foreach (var gesture in gestures) {
            token.ThrowIfCancellationRequested();
            Press(window, gesture, check);
            delivered?.Invoke(++count);
            await Idle(window.Dispatcher, token);
        }
    }
}
