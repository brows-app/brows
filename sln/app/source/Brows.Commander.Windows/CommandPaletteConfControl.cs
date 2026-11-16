using Brows.Gui;
using Brows.Triggers;
using Brows.Windows.Controls;
using System.Windows.Controls;
using System.Windows.Input;

namespace Brows;

internal sealed class CommandPaletteConfControl : ModelView<CommandPaletteConf, ICommandPaletteConfController>,
                                                  ICommandPaletteConfController {
    private event GestureEventHandler _Gesture;

    private TextBox _TextBox;

    private void This_PreviewKeyDown(object sender, KeyEventArgs e) {
        if (e is not null) {
            e.Trigger(a => _Gesture?.Invoke(this, a));
        }
    }

    private TextBox GetTextBox() {
        return GetTemplateChild("PART_Text") as TextBox;
    }

    public CommandPaletteConfControl() {
    }

    public override void OnApplyTemplate() {
        if (_TextBox is not null) {
            _TextBox.PreviewKeyDown -= This_PreviewKeyDown;
        }
        base.OnApplyTemplate();
        _TextBox = GetTextBox();
        if (_TextBox is not null) {
            _TextBox.PreviewKeyDown += This_PreviewKeyDown;
        }
    }

    event GestureEventHandler ICommandPaletteConfController.Gesture {
        add {
            _Gesture += value;
        }
        remove {
            _Gesture -= value;
        }
    }

    void ICommandPaletteConfController.Focus() {
        GetTextBox()?.Focus();
    }

    int ICommandPaletteConfController.CaretLine() {
        var textBox = GetTextBox();
        return textBox is not null
            ? textBox.GetLineIndexFromCharacterIndex(textBox.CaretIndex)
            : -1;
    }
}
