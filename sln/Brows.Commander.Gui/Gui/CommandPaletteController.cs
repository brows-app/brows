using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace Brows.Gui {
    internal class CommandPaletteController : TriggerController<ICommandPaletteController>, ICommandPaletteController {
        private void Items_CurrentChanged(object sender, EventArgs e) {
            CurrentSuggestionChanged?.Invoke(this, e);
        }

        private void UserControl_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (UserControl.IsKeyboardFocusWithin == false) {
                var window = Window.GetWindow(UserControl);
                var lostFocus = window == null || window.IsKeyboardFocusWithin;
                if (lostFocus) {
                    LostFocus?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler CurrentSuggestionChanged;
        public event EventHandler LostFocus;

        public new CommandPaletteControl UserControl { get; }

        public CommandPaletteController(CommandPaletteControl userControl) : base(userControl) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
            UserControl.IsKeyboardFocusWithinChanged += UserControl_IsKeyboardFocusWithinChanged;
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
    }
}
