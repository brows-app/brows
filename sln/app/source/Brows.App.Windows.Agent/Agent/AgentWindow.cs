using Brows.Windows.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Brows.Agent;

/*
 * Identifies windows and describes the parts of their state that an agent checks before it acts.
 */
internal static class AgentWindow {
    private static DependencyObject Parent(DependencyObject element) {
        var elementIsVisual = element is Visual or Visual3D;
        if (elementIsVisual) {
            var parent = VisualTreeHelper.GetParent(element);
            if (parent is not null) {
                return parent;
            }
        }
        return LogicalTreeHelper.GetParent(element);
    }

    internal static long Id(Window window) {
        ArgumentNullException.ThrowIfNull(window);
        return new WindowInteropHelper(window).Handle.ToInt64();
    }

    internal static bool Contains(Window window, IInputElement element) {
        return element is DependencyObject obj && ReferenceEquals(Window.GetWindow(obj), window);
    }

    internal static IInputElement KeyboardFocus(Window window) {
        var focused = Keyboard.FocusedElement;
        return Contains(window, focused)
            ? focused
            : null;
    }

    internal static AgentWindowInfo Info(Window window, int index) {
        ArgumentNullException.ThrowIfNull(window);
        return new AgentWindowInfo(
            Id: Id(window),
            Index: index,
            Title: window.Title,
            Active: window.IsActive,
            Width: (int)Math.Round(window.ActualWidth),
            Height: (int)Math.Round(window.ActualHeight));
    }

    internal static AgentFocus Focus(Window window) {
        if (KeyboardFocus(window) is not DependencyObject focused) {
            return null;
        }
        var name = (focused as FrameworkElement)?.Name;
        return new AgentFocus(
            Type: focused.GetType().Name,
            Name: string.IsNullOrEmpty(name) ? null : name,
            Owner: Owner(focused)?.GetType().Name);
    }

    internal static AgentPalette Palette(Window window) {
        var input = Find<CommandPaletteInputControl>(window);
        if (input is null) {
            return new AgentPalette(Open: false, Focused: false, Text: null);
        }
        var text = input.Template?.FindName("PART_Text", input) as TextBox;
        return new AgentPalette(Open: true, Focused: text?.IsKeyboardFocused is true, Text: text?.Text);
    }

    internal static ModelView Owner(DependencyObject element) {
        var current = element is null ? null : Parent(element);
        while (current is not null) {
            if (current is ModelView view) {
                return view;
            }
            current = Parent(current);
        }
        return null;
    }

    internal static T Find<T>(DependencyObject root) where T : DependencyObject {
        if (root is null) {
            return null;
        }
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++) {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found) {
                return found;
            }
            var descendant = Find<T>(child);
            if (descendant is not null) {
                return descendant;
            }
        }
        return null;
    }
}
