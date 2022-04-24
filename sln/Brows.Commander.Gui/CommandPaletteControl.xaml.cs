using System.Windows;
using System.Windows.Input;

namespace Brows {
    using Gui;

    partial class CommandPaletteControl {
        private void CommandSuggestionListView_GotFocus(object sender, RoutedEventArgs e) {
            InputTextBox.Focus();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e) {
            if (e != null) {
                switch (e.Key) {
                    case Key.Down: {
                            var items = CommandSuggestionListView.Items;
                            if (items.CurrentPosition < 0) {
                                items.MoveCurrentToFirst();
                            }
                            else {
                                if (items.CurrentPosition < items.Count - 1) {
                                    items.MoveCurrentToNext();
                                }
                            }
                            InputTextBox.CaretIndex = InputTextBox.Text.Length;
                            e.Handled = true;
                            break;
                        }
                    case Key.Up: {
                            var items = CommandSuggestionListView.Items;
                            if (items.CurrentPosition < 0) {
                                items.MoveCurrentToFirst();
                            }
                            else {
                                if (items.CurrentPosition > 0) {
                                    items.MoveCurrentToPrevious();
                                }
                            }
                            InputTextBox.CaretIndex = InputTextBox.Text.Length;
                            e.Handled = true;
                            break;
                        }
                    case Key.PageDown:
                    case Key.PageUp:
                        var args = new KeyEventArgs(e.KeyboardDevice, e.InputSource, e.Timestamp, e.Key) {
                            RoutedEvent = KeyDownEvent
                        };
                        CommandSuggestionListView.RaiseEvent(args);
                        InputTextBox.CaretIndex = InputTextBox.Text.Length;
                        e.Handled = true;
                        break;
                }
            }
            base.OnPreviewKeyDown(e);
        }

        public CommandPaletteControl() {
            InitializeComponent();
            new CommandPaletteController(this);
        }
    }
}
