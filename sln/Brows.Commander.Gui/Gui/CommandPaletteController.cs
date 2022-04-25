using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Brows.Gui {
    using Triggers;
    using Windows.Input;


    internal class CommandPaletteController : Controller<ICommandPaletteController>, ICommandPaletteController {
        private void Items_CurrentChanged(object sender, EventArgs e) {
            CurrentSuggestionChanged?.Invoke(this, e);
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
            var key = (KeyboardKey)e.ReferencedKey();
            var modifiers = (KeyboardModifiers)e.ModifierKeys();
            var eventArgs = new KeyboardKeyEventArgs(key, modifiers);
            var eventHandler = KeyboardKeyDown;
            if (eventHandler != null) {
                eventHandler.Invoke(this, eventArgs);
            }
            var handled = e.Handled = eventArgs.Triggered;
            if (handled == false) {
                switch (e.Key) {
                    case Key.Down: {
                            var items = UserControl.CommandSuggestionListView.Items;
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
                            var items = UserControl.CommandSuggestionListView.Items;
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
                            RoutedEvent = UIElement.KeyDownEvent
                        };
                        UserControl.CommandSuggestionListView.RaiseEvent(args);
                        InputTextBox.CaretIndex = InputTextBox.Text.Length;
                        e.Handled = true;
                        break;
                }
            }
        }

        public event EventHandler CurrentSuggestionChanged;
        public event EventHandler LostFocus;
        public event InputEventHandler Input;
        public event KeyboardKeyEventHandler KeyboardKeyDown;

        public CommandPaletteInputTextBox InputTextBox => UserControl.InputTextBox;
        public new CommandPaletteControl UserControl { get; }

        public CommandPaletteController(CommandPaletteControl userControl) : base(userControl) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
            UserControl.InputTextBox.IsKeyboardFocusWithinChanged += InputTextBox_IsKeyboardFocusWithinChanged;
            UserControl.InputTextBox.PreviewKeyDown += InputTextBox_PreviewKeyDown;
            UserControl.CommandSuggestionListView.Items.CurrentChanged += Items_CurrentChanged;
            UserControl.CommandSuggestionListView.Items.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ICommandSuggestion.Header)));
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

        void ICommandPaletteController.ScrollSuggestionData(KeyboardKey key) {
            var listBox = UserControl?.SuggestionDataControl?.FindVisualChild<ListBox>();
            var scrollViewer = UserControl?.SuggestionDataControl?.FindVisualChild<ScrollViewer>();
            switch (key) {
                case KeyboardKey.Down:
                    if (listBox != null) {
                        listBox.MoveToNextOne();
                    }
                    else {
                        scrollViewer?.ScrollToVerticalOffset(scrollViewer.VerticalOffset + 1);
                    }
                    break;
                case KeyboardKey.Up:
                    if (listBox != null) {
                        listBox.MoveToPreviousOne();
                    }
                    else {
                        scrollViewer?.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 1);
                    }
                    break;
                case KeyboardKey.PageDown:
                    if (listBox != null) {
                        if (scrollViewer != null) {
                            listBox.MoveToOffset((int)scrollViewer.ViewportHeight);
                        }
                    }
                    else {
                        scrollViewer?.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollViewer.ViewportHeight);
                    }
                    break;
                case KeyboardKey.PageUp:
                    if (listBox != null) {
                        if (scrollViewer != null) {
                            listBox.MoveToOffset(-(int)scrollViewer.ViewportHeight);
                        }
                    }
                    else {
                        scrollViewer?.ScrollToVerticalOffset(scrollViewer.VerticalOffset - scrollViewer.ViewportHeight);
                    }
                    break;
            }
        }
    }
}
