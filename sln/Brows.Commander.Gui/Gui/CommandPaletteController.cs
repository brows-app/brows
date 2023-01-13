using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Brows.Gui {
    using Windows.Input;

    internal class CommandPaletteController : Controller<ICommandPaletteController>, ICommandPaletteController {
        private void Items_CurrentChanged(object sender, EventArgs e) {
            CurrentSuggestionChanged?.Invoke(this, e);
        }

        private void InputTextBox_Loaded(object sender, RoutedEventArgs e) {
            if (InputTextBox.SelectionLength == 0) {
                InputTextBox.CaretIndex = InputTextBox.Text.Length;
            }
            InputTextBox.Focus();
        }

        private void InputTextBox_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (InputTextBox.IsKeyboardFocusWithin == false) {
                var window = Window.GetWindow(InputTextBox);
                var lostFocus = window == null || window.IsKeyboardFocusWithin;
                if (lostFocus) {
                    LostFocus?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e == null) return;
            if (e.OriginalSource != InputTextBox) {
                return;
            }
            var key = (PressKey)e.ReferencedKey();
            var modifiers = (PressModifiers)e.ModifierKeys();
            var eventArgs = new CommanderPressEventArgs(key, modifiers);
            var eventHandler = KeyboardKeyDown;
            if (eventHandler != null) {
                eventHandler.Invoke(this, eventArgs);
            }
            var handled = e.Handled = eventArgs.Triggered;
            if (handled == false) {
                switch (e.Key) {
                    case Key.Up:
                        UserControl.CommandSuggestionListView.MoveUp();
                        handled = true;
                        break;
                    case Key.Down:
                        UserControl.CommandSuggestionListView.MoveDown();
                        handled = true;
                        break;
                    case Key.PageUp:
                        UserControl.CommandSuggestionListView.MovePageUp();
                        handled = true;
                        break;
                    case Key.PageDown:
                        UserControl.CommandSuggestionListView.MovePageDown();
                        handled = true;
                        break;
                }
                if (handled) {
                    e.Handled = true;
                    InputTextBox.CaretIndex = InputTextBox.Text.Length;
                }
            }
        }

        public event EventHandler CurrentSuggestionChanged;
        public event EventHandler LostFocus;
        public event CommanderPressEventHandler KeyboardKeyDown;

        public CommandPaletteInputTextBox InputTextBox => UserControl.InputTextBox;
        public new CommandPaletteControl UserControl { get; }

        public CommandPaletteController(CommandPaletteControl userControl) : base(userControl) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
            UserControl.InputTextBox.IsKeyboardFocusWithinChanged += InputTextBox_IsKeyboardFocusWithinChanged;
            UserControl.InputTextBox.Loaded += InputTextBox_Loaded;
            UserControl.InputTextBox.PreviewKeyDown += InputTextBox_PreviewKeyDown;
            UserControl.CommandSuggestionListView.Items.CurrentChanged += Items_CurrentChanged;
            UserControl.CommandSuggestionListView.Items.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ICommandSuggestion.Group)));
            UserControl.CommandSuggestionListView.Items.IsLiveSorting = true;
            UserControl.CommandSuggestionListView.Items.LiveSortingProperties.Add(nameof(ICommandSuggestion.Relevance));
            UserControl.CommandSuggestionListView.Items.LiveSortingProperties.Add(nameof(ICommandSuggestion.Input));
            UserControl.CommandSuggestionListView.Items.SortDescriptions.Add(new SortDescription(nameof(ICommandSuggestion.Relevance), ListSortDirection.Ascending));
            UserControl.CommandSuggestionListView.Items.SortDescriptions.Add(new SortDescription(nameof(ICommandSuggestion.Input), ListSortDirection.Ascending));
        }

        ICommandSuggestion ICommandPaletteController.CurrentSuggestion =>
            UserControl.CommandSuggestionListView.Items.CurrentItem as ICommandSuggestion;

        void ICommandPaletteController.MoveCaret(int index) {
            UserControl.InputTextBox.CaretIndex = index;
        }

        void ICommandPaletteController.SelectText(int start, int length) {
            UserControl.InputTextBox.Select(start, length);
        }

        void ICommandPaletteController.ScrollSuggestionData(PressKey key) {
            var listBox = UserControl?.SuggestionDataControl?.FindVisualChild<ListBox>();
            var scrollViewer = UserControl?.SuggestionDataControl?.FindVisualChild<ScrollViewer>();
            switch (key) {
                case PressKey.Down:
                    if (listBox != null) {
                        listBox.MoveDown();
                    }
                    else {
                        scrollViewer?.LineDown();
                    }
                    break;
                case PressKey.Up:
                    if (listBox != null) {
                        listBox.MoveUp();
                    }
                    else {
                        scrollViewer?.LineUp();
                    }
                    break;
                case PressKey.PageDown:
                    if (listBox != null) {
                        listBox.MovePageDown();
                    }
                    else {
                        scrollViewer?.PageDown();
                    }
                    break;
                case PressKey.PageUp:
                    if (listBox != null) {
                        listBox.MovePageUp();
                    }
                    else {
                        scrollViewer?.PageUp();
                    }
                    break;
            }
        }
    }
}
