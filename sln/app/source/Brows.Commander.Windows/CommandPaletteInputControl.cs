using Brows.Commands;
using Brows.Gui;
using Brows.Triggers;
using Brows.Windows.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Brows;

internal sealed class CommandPaletteInputControl : ModelView<ICommandPalette, ICommandPaletteInputController>,
                                                   ICommandPaletteInputController {
    private event EventHandler _Loaded;
    private event EventHandler _Unloaded;
    private event GestureEventHandler _Gesture;

    private TextBox _TextBox;
    private TextBox _TextSuggestion;

    private void TextBox_ScrollChanged(object sender, ScrollChangedEventArgs e) {
        var textBoxesReady = _TextBox is not null && _TextSuggestion is not null;
        if (textBoxesReady) {
            if (_TextSuggestion.HorizontalOffset != _TextBox.HorizontalOffset) {
                _TextSuggestion.ScrollToHorizontalOffset(_TextBox.HorizontalOffset);
            }
        }
    }

    private void This_Loaded(object sender, RoutedEventArgs e) {
        _Loaded?.Invoke(this, e);
        var textBox = GetTextBox();
        if (textBox is null) {
            return;
        }
        if (textBox.SelectionLength == 0) {
            textBox.CaretIndex = textBox.Text.Length;
        }
        textBox.Focus();
    }

    private void This_Unloaded(object sender, RoutedEventArgs e) {
        _Unloaded?.Invoke(this, e);
    }

    private void This_PreviewKeyDown(object sender, KeyEventArgs e) {
        if (e is not null) {
            e.Trigger(a => _Gesture?.Invoke(this, a));
        }
    }

    private TextBox GetTextBox() {
        return GetTemplateChild("PART_Text") as TextBox;
    }

    public CommandPaletteInputControl() {
        Loaded += This_Loaded;
        Unloaded += This_Unloaded;
    }

    public override void OnApplyTemplate() {
        if (_TextBox is not null) {
            _TextBox.PreviewKeyDown -= This_PreviewKeyDown;
            _TextBox.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(TextBox_ScrollChanged));
        }
        if (_TextSuggestion is not null) {
            _TextSuggestion.RemoveHandler(
                ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(TextBox_ScrollChanged));
        }
        base.OnApplyTemplate();
        _TextBox = GetTextBox();
        _TextSuggestion = GetTemplateChild("PART_TextSuggestion") as TextBox;
        if (_TextBox is not null) {
            _TextBox.PreviewKeyDown += This_PreviewKeyDown;
            _TextBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(TextBox_ScrollChanged));
        }
        if (_TextSuggestion is not null) {
            _TextSuggestion.AddHandler(
                ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(TextBox_ScrollChanged));
        }
    }

    event GestureEventHandler ICommandPaletteInputController.Gesture {
        add {
            _Gesture += value;
        }
        remove {
            _Gesture -= value;
        }
    }

    event EventHandler ICommandPaletteInputController.Loaded {
        add {
            _Loaded += value;
        }
        remove {
            _Loaded -= value;
        }
    }

    event EventHandler ICommandPaletteInputController.Unloaded {
        add {
            _Unloaded += value;
        }
        remove {
            _Unloaded -= value;
        }
    }

    void ICommandPaletteInputController.MoveCaret(int index) {
        var textBox = GetTextBox();
        if (textBox is not null) {
            textBox.CaretIndex = index;
        }
    }

    void ICommandPaletteInputController.SelectText(int start, int length) {
        var textBox = GetTextBox();
        if (textBox is not null) {
            textBox.Select(start, length);
        }
    }

    void ICommandPaletteInputController.Focus() {
        GetTextBox()?.Focus();
    }
}
