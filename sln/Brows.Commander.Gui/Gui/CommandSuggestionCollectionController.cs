using System;
using System.ComponentModel;
using System.Windows.Data;

namespace Brows.Gui {
    internal sealed class CommandSuggestionCollectionController : Controller<ICommandSuggestionCollectionController>, ICommandSuggestionCollectionController {
        private void Items_CurrentChanged(object sender, EventArgs e) {
            CurrentSuggestionChanged?.Invoke(this, e);
        }

        public event EventHandler CurrentSuggestionChanged;

        public new CommandSuggestionCollectionListView Element { get; }

        public CommandSuggestionCollectionController(CommandSuggestionCollectionListView element) : base(element) {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Element.Items.CurrentChanged += Items_CurrentChanged;
            Element.Items.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ICommandSuggestion.Group)));
            Element.Items.IsLiveSorting = true;
            Element.Items.LiveSortingProperties.Add(nameof(ICommandSuggestion.Relevance));
            Element.Items.LiveSortingProperties.Add(nameof(ICommandSuggestion.Input));
            Element.Items.SortDescriptions.Add(new SortDescription(nameof(ICommandSuggestion.Relevance), ListSortDirection.Ascending));
            Element.Items.SortDescriptions.Add(new SortDescription(nameof(ICommandSuggestion.Input), ListSortDirection.Ascending));
        }

        ICommandSuggestion ICommandSuggestionCollectionController.CurrentSuggestion =>
            Element.Items.CurrentItem as ICommandSuggestion;

        void ICommandSuggestionCollectionController.MoveCurrentSuggestion(PressKey pressKey) {
            Element.Move(pressKey);
        }
    }
}
