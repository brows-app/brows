using Brows.Commands;
using Brows.Gui;
using Brows.Triggers;
using Brows.Windows.Controls;
using Domore.Logs;
using InputEventArgs = Brows.Triggers.InputEventArgs;
using InputEventHandler = Brows.Triggers.InputEventHandler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Brows.Windows;

namespace Brows;

internal sealed class CommanderControl : ModelView<ICommander, ICommanderController>, ICommanderController {
    private static readonly ILog Log = Logging.For(typeof(CommanderControl));
    private Task WindowCloseTask;
    private bool WindowCloseAllowed;
    private long WindowAttachmentGeneration;

    private readonly ConsumedInputKeyState _ConsumedInputKeyState = new();

    private event EventHandler _Loaded;
    private event EventHandler _WindowClosed;
    private event EventHandler _WindowClosing;
    private event InputEventHandler _WindowInput;
    private event GestureEventHandler _WindowGesture;

    private Window _Window;
    private IntPtr _WindowHandle;

    private static Key PhysicalKey(KeyEventArgs keyEventArgs) {
        return keyEventArgs.Key switch {
            Key.DeadCharProcessed => keyEventArgs.DeadCharProcessedKey,
            Key.ImeProcessed => keyEventArgs.ImeProcessedKey,
            Key.System => keyEventArgs.SystemKey,
            _ => keyEventArgs.Key
        };
    }

    private static DependencyObject InputParent(DependencyObject element) {
        if (element is ContentElement contentElement) {
            var contentParent = ContentOperations.GetParent(contentElement);
            if (contentParent is not null) {
                return contentParent;
            }
        }
        if (element is FrameworkContentElement frameworkContentElement) {
            var contentParent = frameworkContentElement.Parent;
            if (contentParent is not null) {
                return contentParent;
            }
        }
        var elementIsVisual = element is Visual or Visual3D;
        if (elementIsVisual) {
            var visualParent = VisualTreeHelper.GetParent(element);
            if (visualParent is not null) {
                return visualParent;
            }
        }
        return LogicalTreeHelper.GetParent(element);
    }

    private static object FindInputOwner(DependencyObject element, out bool ownerWasExplicitlyCleared) {
        ownerWasExplicitlyCleared = false;
        var visited = new HashSet<DependencyObject>();
        var current = element;
        while (current is not null) {
            var currentIsNew = visited.Add(current);
            if (currentIsNew == false) {
                break;
            }
            var owner = InputOwner.GetOwner(current);
            if (owner is not null) {
                return owner;
            }
            var ownerValueSource = DependencyPropertyHelper.GetValueSource(current, InputOwner.OwnerProperty);
            var ownerIsExplicit = ownerValueSource.BaseValueSource is not
                (BaseValueSource.Default or BaseValueSource.Inherited);
            if (ownerIsExplicit) {
                ownerWasExplicitlyCleared = true;
                return null;
            }
            current = InputParent(current);
        }
        return null;
    }

    private static Window FindWindow(DependencyObject element) {
        var visited = new HashSet<DependencyObject>();
        var current = element;
        while (current is not null) {
            var currentIsNew = visited.Add(current);
            if (currentIsNew == false) {
                break;
            }
            if (current is Window window) {
                return window;
            }
            current = InputParent(current);
        }
        return null;
    }

    private static object LegacySource(object source) {
        return source is FrameworkElement element
            ? element.DataContext
            : null;
    }

    private void Window_Closed(object sender, EventArgs e) {
        _ConsumedInputKeyState.Clear();
        _WindowClosed?.Invoke(this, e);
    }

    private void Window_Closing(object sender, CancelEventArgs e) {
        var lifetime = ViewModel as IAsyncDisposable;
        var mustAwaitCleanup = WindowCloseAllowed == false && lifetime is not null;
        if (mustAwaitCleanup) {
            e.Cancel = true;
            WindowCloseTask ??= CloseWindowAsync(_Window, ViewModel, lifetime, WindowAttachmentGeneration);
            return;
        }
        _WindowClosing?.Invoke(this, e);
    }

    private async Task CloseWindowAsync(Window window, ICommander model, IAsyncDisposable lifetime,
                                       long attachmentGeneration) {
        await Task.Yield();
        try {
            await lifetime.DisposeAsync();
            var attachmentIsCurrent = ReferenceEquals(window, _Window) && ReferenceEquals(model, ViewModel) &&
                    attachmentGeneration == WindowAttachmentGeneration;
            if (attachmentIsCurrent) {
                WindowCloseAllowed = true;
                window.Close();
            }
        }
        catch (Exception exception) {
            if (attachmentGeneration == WindowAttachmentGeneration) {
                WindowCloseTask = null;
            }
            Log.Error(nameof(CloseWindowAsync), exception);
        }
    }
    private void Window_Deactivated(object sender, EventArgs e) {
        _ConsumedInputKeyState.Clear();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
        if (e is null) {
            return;
        }
        var key = PhysicalKey(e);
        var consumed = _ConsumedInputKeyState.TrySuppressKeyDown(key);
        if (e.Handled) {
            return;
        }
        if (consumed) {
            e.Handled = true;
            return;
        }

        var stateGeneration = _ConsumedInputKeyState.Generation;
        var source = ResolveKeyboardSource(e.OriginalSource, Keyboard.FocusedElement as DependencyObject, _Window);
        var modifiers = (PressModifiers)(e.KeyboardDevice?.Modifiers ?? ModifierKeys.None);
        var gesture = new GestureEventArgs(new PressGesture((PressKey)key, modifiers), source);
        _WindowGesture?.Invoke(this, gesture);
        if (gesture.Triggered) {
            e.Handled = true;
            var sourceIsScoped = source is IControllingCommandGesture;
            var inputStateIsUnchanged = _ConsumedInputKeyState.Generation == stateGeneration;
            var canReserveScopedKey = sourceIsScoped && inputStateIsUnchanged;
            if (canReserveScopedKey) {
                _ConsumedInputKeyState.RecordConsumedKeyDown(key);
            }
            return;
        }
        if (e.Key == Key.Space) {
            var input = new InputEventArgs(" ", source);
            _WindowInput?.Invoke(this, input);
            if (input.Triggered) {
                e.Handled = true;
            }
        }
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e) {
        if (e is null) {
            return;
        }
        var consumed = _ConsumedInputKeyState.TryReleaseKeyUp(PhysicalKey(e));
        if (e.Handled) {
            return;
        }
        if (consumed) {
            e.Handled = true;
        }
    }

    private void Window_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e) {
        if (e is not null) {
            e.Trigger(clicks: 2, a => _WindowGesture?.Invoke(this, a));
        }
    }

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e) {
        if (e is null) {
            return;
        }
        var isComposingText = e.TextComposition.AutoComplete == TextCompositionAutoComplete.Off;
        var consumed = _ConsumedInputKeyState.TrySuppressTextInput(isComposingText);
        if (e.Handled) {
            return;
        }
        if (consumed) {
            e.Handled = true;
            return;
        }
        var source = ResolveKeyboardSource(e.OriginalSource, Keyboard.FocusedElement as DependencyObject, _Window);
        var input = new InputEventArgs(e.Text, source);
        _WindowInput?.Invoke(this, input);
        if (input.Triggered) {
            e.Handled = true;
        }
    }

    private void This_Loaded(object sender, RoutedEventArgs e) {
        var newWindow = Window.GetWindow(this);
        var oldWindow = _Window;
        if (oldWindow != newWindow) {
            WindowAttachmentGeneration++;
            WindowCloseAllowed = false;
            WindowCloseTask = null;
            _ConsumedInputKeyState.Clear();
            if (oldWindow is not null) {
                oldWindow.Closed -= Window_Closed;
                oldWindow.Closing -= Window_Closing;
                oldWindow.Deactivated -= Window_Deactivated;
                oldWindow.RemoveHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(Window_PreviewKeyDown));
                oldWindow.RemoveHandler(Keyboard.PreviewKeyUpEvent, new KeyEventHandler(Window_PreviewKeyUp));
                oldWindow.RemoveHandler(
                    TextCompositionManager.PreviewTextInputEvent,
                    new TextCompositionEventHandler(Window_PreviewTextInput));
                oldWindow.PreviewMouseDoubleClick -= Window_PreviewMouseDoubleClick;
            }
            if (newWindow is not null) {
                newWindow.Closed += Window_Closed;
                newWindow.Closing += Window_Closing;
                newWindow.Deactivated += Window_Deactivated;
                newWindow.AddHandler(
                    Keyboard.PreviewKeyDownEvent,
                    new KeyEventHandler(Window_PreviewKeyDown),
                    handledEventsToo: true);
                newWindow.AddHandler(
                    Keyboard.PreviewKeyUpEvent,
                    new KeyEventHandler(Window_PreviewKeyUp),
                    handledEventsToo: true);
                newWindow.AddHandler(
                    TextCompositionManager.PreviewTextInputEvent,
                    new TextCompositionEventHandler(Window_PreviewTextInput),
                    handledEventsToo: true);
                newWindow.PreviewMouseDoubleClick += Window_PreviewMouseDoubleClick;
            }
        }
        _Window = newWindow;
        _WindowHandle = newWindow is not null
            ? new WindowInteropHelper(newWindow).Handle
            : IntPtr.Zero;
        _Loaded?.Invoke(this, e);
    }

    private void This_Unloaded(object sender, RoutedEventArgs e) {
        WindowAttachmentGeneration++;
        _ConsumedInputKeyState.Clear();
        var window = _Window;
        if (window is not null) {
            window.Closed -= Window_Closed;
            window.Closing -= Window_Closing;
            window.Deactivated -= Window_Deactivated;
            window.RemoveHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(Window_PreviewKeyDown));
            window.RemoveHandler(Keyboard.PreviewKeyUpEvent, new KeyEventHandler(Window_PreviewKeyUp));
            window.RemoveHandler(
                TextCompositionManager.PreviewTextInputEvent,
                new TextCompositionEventHandler(Window_PreviewTextInput));
            window.PreviewMouseDoubleClick -= Window_PreviewMouseDoubleClick;
        }
        _Window = null;
        _WindowHandle = IntPtr.Zero;
    }

    internal static object ResolveKeyboardSource(
        object originalSource,
        DependencyObject focusedElement,
        Window window) {
        if (originalSource is FrameworkElement element) {
            /*
             * Connected elements already inherit their effective owner and window.
             * Read those values directly so normal key and text input needs no tree walk.
             */
            var sourceIsKnownChild = window is not null && element != window &&
                ReferenceEquals(Window.GetWindow(element), window);
            if (sourceIsKnownChild) {
                return InputOwner.GetOwner(element) ?? element.DataContext;
            }
        }
        var sourceElement = originalSource as DependencyObject;
        var sourceWindow = FindWindow(sourceElement);
        var sourceOwner = FindInputOwner(sourceElement, out var sourceOwnerWasExplicitlyCleared);
        var sourceBelongsToWindow = sourceWindow is null || sourceWindow == window;
        var sourceHasOwnerForWindow = sourceOwner is not null && sourceBelongsToWindow;
        if (sourceHasOwnerForWindow) {
            return sourceOwner;
        }
        if (sourceOwnerWasExplicitlyCleared) {
            return LegacySource(originalSource);
        }

        var sourceIsInsufficient = sourceElement is null || sourceWindow is null || sourceElement == window;
        var focusedElementIsInWindow = focusedElement is not null && FindWindow(focusedElement) == window;
        var canUseFocusedFallback = sourceIsInsufficient && focusedElementIsInWindow;
        if (canUseFocusedFallback) {
            var focusedOwner = FindInputOwner(focusedElement, out _);
            if (focusedOwner is not null) {
                return focusedOwner;
            }
            if (sourceElement is null) {
                return LegacySource(focusedElement);
            }
        }
        return LegacySource(originalSource);
    }

    public CommanderControl() {
        Loaded += This_Loaded;
        Unloaded += This_Unloaded;
    }

    event EventHandler ICommanderController.Loaded {
        add {
            _Loaded += value;
        }
        remove {
            _Loaded -= value;
        }
    }

    event EventHandler ICommanderController.WindowClosed {
        add {
            _WindowClosed += value;
        }
        remove {
            _WindowClosed -= value;
        }
    }

    event EventHandler ICommanderController.WindowClosing {
        add {
            _WindowClosing += value;
        }
        remove {
            _WindowClosing -= value;
        }
    }

    event InputEventHandler ICommanderController.WindowInput {
        add {
            _WindowInput += value;
        }
        remove {
            _WindowInput -= value;
        }
    }

    event GestureEventHandler ICommanderController.WindowGesture {
        add {
            _WindowGesture += value;
        }
        remove {
            _WindowGesture -= value;
        }
    }

    void ICommanderController.CloseWindow() {
        _Window?.Close();
    }

    object ICommanderController.NativeWindow() {
        return _WindowHandle == IntPtr.Zero
            ? default(object)
            : _WindowHandle;
    }
}
