using Brows.Commands;
using Brows.Windows.Controls;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using MODIFIERKEYS = System.Windows.Input.ModifierKeys;

namespace Brows.Triggers;

public static class TriggerPressEvent {
    private static MODIFIERKEYS ModifierKeys(this KeyEventArgs keyEventArgs) {
        if (keyEventArgs is null) {
            throw new ArgumentNullException(nameof(keyEventArgs));
        }
        return keyEventArgs.KeyboardDevice?.Modifiers ?? MODIFIERKEYS.None;
    }

    public static bool Trigger(this KeyEventArgs keyEventArgs, Action<GestureEventArgs> handler) {
        if (keyEventArgs is null) {
            throw new ArgumentNullException(nameof(keyEventArgs));
        }
        if (handler is not null) {
            var args = gesture(keyEventArgs); handler(args);
            if (args.Triggered) {
                return keyEventArgs.Handled = true;
            }
            static GestureEventArgs gesture(KeyEventArgs keyEventArgs) {
                if (keyEventArgs is null) {
                    throw new ArgumentNullException(nameof(keyEventArgs));
                }
                return new GestureEventArgs(
                    source: keyEventArgs.OriginalSource is FrameworkElement element
                        ? element.DataContext
                        : null,
                    gesture: new PressGesture(
                        key: (PressKey)referencedKey(keyEventArgs),
                        modifiers: (PressModifiers)ModifierKeys(keyEventArgs)));
                static Key referencedKey(KeyEventArgs keyEventArgs) {
                    if (keyEventArgs is null) {
                        throw new ArgumentNullException(nameof(keyEventArgs));
                    }
                    return keyEventArgs.Key switch {
                        Key.DeadCharProcessed => keyEventArgs.DeadCharProcessedKey,
                        Key.ImeProcessed => keyEventArgs.ImeProcessedKey,
                        Key.System => keyEventArgs.SystemKey,
                        _ => keyEventArgs.Key,
                    };
                }
            }
        }
        return false;
    }

    public static bool Trigger(this MouseButtonEventArgs mouseButtonEventArgs, int clicks, Action<GestureEventArgs> handler) {
        if (mouseButtonEventArgs is null) {
            throw new ArgumentNullException(nameof(mouseButtonEventArgs));
        }
        if (handler is not null) {
            var e = mouseButtonEventArgs;
            var source = clickSource(e.OriginalSource);
            var button = (ClickButton)e.ChangedButton;
            var modifiers = (ClickModifiers)Keyboard.Modifiers;
            var eventArgs = new GestureEventArgs(new ClickGesture(button, modifiers, clicks), source);
            var args = eventArgs;
            {
                handler(args);
            }
            if (args.Triggered) {
                return mouseButtonEventArgs.Handled = true;
            }
            static object clickSource(object originalSource) {
                /*
                 * Templates commonly set their DataContext to the templated control, so the
                 * clicked element's DataContext may be a control rather than a model. Use the
                 * nearest command source, whether it is a view's typed model or a DataContext.
                 */
                var visited = new HashSet<DependencyObject>();
                var current = originalSource as DependencyObject;
                while (current is not null) {
                    var currentIsNew = visited.Add(current);
                    if (currentIsNew == false) {
                        break;
                    }
                    if (current is ModelView view) {
                        var viewModel = view.ViewModelObject;
                        if (viewModel is ICommandSourceObject) {
                            return viewModel;
                        }
                    }
                    if (current is FrameworkElement frameworkElement) {
                        var dataContext = frameworkElement.DataContext;
                        if (dataContext is ICommandSourceObject) {
                            return dataContext;
                        }
                    }
                    current = clickParent(current);
                }
                return originalSource is FrameworkElement element
                    ? element.DataContext
                    : null;
                static DependencyObject clickParent(DependencyObject element) {
                    var elementIsVisual = element is Visual or Visual3D;
                    if (elementIsVisual) {
                        var visualParent = VisualTreeHelper.GetParent(element);
                        if (visualParent is not null) {
                            return visualParent;
                        }
                    }
                    return LogicalTreeHelper.GetParent(element);
                }
            }
        }
        return false;
    }
}
