using Brows.Windows.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;

namespace Brows.Agent;

/*
 * Writes the visible text of a window as indented lines, so an agent can read what a person would see.
 * Brows views (ModelView) give the lines their structure, and list items are written one line each.
 */
internal static class AgentUiText {
    private const string Indent = "  ";
    private const string LineBreak = "⏎";

    /*
     * Keeps text on one line. Backslashes and quotes are kept as they are, so that paths can be typed back
     * exactly as they appear; each quoted text ends its line, so its quotes need no escaping.
     */
    private static string Escape(string text) {
        return text
            .Replace("\r\n", LineBreak)
            .Replace("\n", LineBreak)
            .Replace("\r", LineBreak)
            .Replace("\t", " ");
    }

    private static string Size(FrameworkElement element) {
        var width = Math.Round(element.ActualWidth).ToString(CultureInfo.InvariantCulture);
        var height = Math.Round(element.ActualHeight).ToString(CultureInfo.InvariantCulture);
        return $"{width}x{height}";
    }

    private static string Text(TextBlock textBlock) {
        var text = textBlock.Text;
        var textIsInInlines = string.IsNullOrEmpty(text) && textBlock.Inlines.Count > 0;
        if (textIsInInlines) {
            text = new TextRange(textBlock.ContentStart, textBlock.ContentEnd).Text;
        }
        return text;
    }

    private static string FocusLine(Window window) {
        var focus = AgentWindow.Focus(window);
        if (focus is null) {
            return "focus none";
        }
        var name = string.IsNullOrEmpty(focus.Name) ? "" : $" {focus.Name}";
        var owner = string.IsNullOrEmpty(focus.Owner) ? "" : $" in {focus.Owner}";
        return $"focus {focus.Type}{name}{owner}";
    }

    internal const int DefaultMaxLines = 400;

    internal static string Write(Window window, int maxLines = DefaultMaxLines) {
        ArgumentNullException.ThrowIfNull(window);
        if (maxLines <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxLines), maxLines, "Use a positive number of lines.");
        }
        var title = Escape(window.Title ?? "");
        var active = window.IsActive ? " active" : "";
        var lines = new List<string> {
            $"window {AgentWindow.Id(window)} \"{title}\"{active} {Size(window)}",
            FocusLine(window)
        };
        var root = VisualTreeHelper.GetChildrenCount(window) > 0
            ? VisualTreeHelper.GetChild(window, 0) as FrameworkElement
            : null;
        if (root is not null) {
            var clip = new Rect(0, 0, root.ActualWidth, root.ActualHeight);
            new Walker(window, lines).Walk(root, 0, clip);
        }
        if (lines.Count <= maxLines) {
            return string.Join("\n", lines);
        }
        var more = lines.Count - maxLines;
        return string.Join("\n", lines.Take(maxLines).Append($"… {more} more lines"));
    }

    private sealed class Walker {
        private Window Window { get; }
        private List<string> Lines { get; }

        private bool TryBounds(DependencyObject element, out Rect bounds) {
            bounds = Rect.Empty;
            if (element is not FrameworkElement frameworkElement) {
                return false;
            }
            try {
                var size = new Rect(0, 0, frameworkElement.ActualWidth, frameworkElement.ActualHeight);
                bounds = frameworkElement.TransformToAncestor(Window).TransformBounds(size);
                return true;
            }
            catch (InvalidOperationException) {
                return false;
            }
        }

        private bool IsShown(DependencyObject element, Rect clip) {
            if (element is UIElement uiElement) {
                var hidden = !uiElement.IsVisible || uiElement.Opacity <= 0;
                if (hidden) {
                    return false;
                }
            }
            if (TryBounds(element, out var bounds)) {
                return bounds.IntersectsWith(clip);
            }
            return true;
        }

        private Rect Clip(DependencyObject element, Rect clip) {
            if (element is ScrollContentPresenter && TryBounds(element, out var bounds)) {
                return Rect.Intersect(clip, bounds);
            }
            return clip;
        }

        private void Texts(DependencyObject element, Rect clip, List<string> texts) {
            if (!IsShown(element, clip)) {
                return;
            }
            if (element is TextBlock textBlock) {
                var text = Text(textBlock);
                if (!string.IsNullOrWhiteSpace(text)) {
                    texts.Add(Escape(text.Trim()));
                }
                return;
            }
            if (element is TextBox textBox) {
                texts.Add(Escape(textBox.Text ?? ""));
                return;
            }
            var childClip = Clip(element, clip);
            var count = VisualTreeHelper.GetChildrenCount(element);
            for (var i = 0; i < count; i++) {
                Texts(VisualTreeHelper.GetChild(element, i), childClip, texts);
            }
        }

        private string GridFields(FrameworkElement presenter, Rect clip) {
            var fields = new List<(double Left, string Text)>();
            var transform = presenter.TransformToAncestor(Window);
            var count = VisualTreeHelper.GetChildrenCount(presenter);
            for (var i = 0; i < count; i++) {
                if (VisualTreeHelper.GetChild(presenter, i) is not FrameworkElement cell) {
                    continue;
                }
                if (presenter is GridViewHeaderRowPresenter &&
                    cell is not GridViewColumnHeader { Role: GridViewColumnHeaderRole.Normal }) {
                    continue;
                }
                var slot = transform.TransformBounds(LayoutInformation.GetLayoutSlot(cell));
                if (slot.Width <= 0 || slot.Left >= clip.Right || slot.Right <= clip.Left ||
                    !slot.IntersectsWith(clip)) {
                    continue;
                }
                var texts = new List<string>();
                Texts(cell, clip, texts);
                fields.Add((slot.Left, string.Join(" ", texts)));
            }
            return string.Join(" | ", fields.OrderBy(field => field.Left).Select(field => field.Text));
        }

        private string Row(ListBoxItem item, Rect clip) {
            var presenter = AgentWindow.Find<GridViewRowPresenter>(item);
            if (presenter is not null) {
                return GridFields(presenter, clip);
            }
            var texts = new List<string>();
            Texts(item, clip, texts);
            return string.Join(" | ", texts);
        }

        private void Add(int depth, string line) {
            Lines.Add(string.Concat(Enumerable.Repeat(Indent, depth)) + line);
        }

        internal Walker(Window window, List<string> lines) {
            Window = window ?? throw new ArgumentNullException(nameof(window));
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
        }

        internal void Walk(DependencyObject element, int depth, Rect clip) {
            if (!IsShown(element, clip)) {
                return;
            }
            switch (element) {
                case ListBoxItem item: {
                    var selected = item.IsSelected ? "[selected] " : "";
                    var focus = item.IsKeyboardFocusWithin ? "[focus] " : "";
                    var row = Row(item, clip);
                    Add(depth, $"> {selected}{focus}{row}".TrimEnd());
                    return;
                }
                case GridViewHeaderRowPresenter header: {
                    var row = GridFields(header, clip);
                    Add(depth, $"| {row}".TrimEnd());
                    return;
                }
                case TextBox textBox: {
                    var name = string.IsNullOrEmpty(textBox.Name) ? "" : $" {textBox.Name}";
                    var line = new StringBuilder($"textbox{name}");
                    if (textBox.IsKeyboardFocused) {
                        line.Append(" [focus]");
                    }
                    line.Append($" \"{Escape(textBox.Text ?? "")}\"");
                    if (textBox.IsKeyboardFocused) {
                        var selection = $"{textBox.SelectionStart}+{textBox.SelectionLength}";
                        line.Append(CultureInfo.InvariantCulture, $" caret {textBox.CaretIndex} selection {selection}");
                    }
                    Add(depth, line.ToString());
                    return;
                }
                case TextBlock textBlock: {
                    var text = Text(textBlock);
                    if (!string.IsNullOrWhiteSpace(text)) {
                        Add(depth, $"\"{Escape(text.Trim())}\"");
                    }
                    return;
                }
                case HwndHost host: {
                    Add(depth, $"native {host.GetType().Name} {Size(host)}");
                    return;
                }
            }
            var childDepth = depth;
            if (element is ModelView view) {
                Add(depth, $"- {view.GetType().Name}");
                childDepth = depth + 1;
            }
            var childClip = Clip(element, clip);
            var count = VisualTreeHelper.GetChildrenCount(element);
            for (var i = 0; i < count; i++) {
                Walk(VisualTreeHelper.GetChild(element, i), childDepth, childClip);
            }
        }
    }
}
