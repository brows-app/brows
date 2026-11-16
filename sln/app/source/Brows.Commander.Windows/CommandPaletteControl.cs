using Brows.Gui;
using Brows.Triggers;
using Brows.Windows.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Brows;

internal sealed class CommandPaletteControl : ModelView<CommandPalette, ICommandPaletteController>,
                                              ICommandPaletteController {
    private event EventHandler _Loaded;
    private event GestureEventHandler _Gesture;

    private void This_Loaded(object sender, RoutedEventArgs e) {
        _Loaded?.Invoke(this, e);
    }

    private void This_PreviewKeyDown(object sender, KeyEventArgs e) {
        if (e is not null) {
            e.Trigger(a => _Gesture?.Invoke(this, a));
        }
    }

    private CommandContextDataControl GetSuggestionDataControl() {
        return GetTemplateChild("PART_SuggestionDataControl") as CommandContextDataControl;
    }

    public CommandPaletteControl() {
        Loaded += This_Loaded;
        PreviewKeyDown += This_PreviewKeyDown;
    }

    event EventHandler IController.Loaded {
        add {
            _Loaded += value;
        }
        remove {
            _Loaded -= value;
        }
    }

    event GestureEventHandler ICommandPaletteController.Gesture {
        add {
            _Gesture += value;
        }
        remove {
            _Gesture -= value;
        }
    }

    void ICommandPaletteController.ScrollSuggestionData(PressKey key) {
        var suggestionDataControl = GetSuggestionDataControl();
        var listBox = suggestionDataControl?.FindVisualChild<ListBox>();
        var scrollViewer = suggestionDataControl?.FindVisualChild<ScrollViewer>();
        switch (key) {
            case PressKey.Down:
                if (listBox is not null) {
                    listBox.MoveDown();
                }
                else {
                    scrollViewer?.LineDown();
                }
                break;
            case PressKey.Up:
                if (listBox is not null) {
                    listBox.MoveUp();
                }
                else {
                    scrollViewer?.LineUp();
                }
                break;
            case PressKey.PageDown:
                if (listBox is not null) {
                    listBox.MovePageDown();
                }
                else {
                    scrollViewer?.PageDown();
                }
                break;
            case PressKey.PageUp:
                if (listBox is not null) {
                    listBox.MovePageUp();
                }
                else {
                    scrollViewer?.PageUp();
                }
                break;
        }
    }
}
