using Brows.Gui;

namespace Brows {
    sealed partial class CommandSuggestionCollectionListView {
        public CommandSuggestionCollectionListView() {
            InitializeComponent();
            new CommandSuggestionCollectionController(this);
        }
    }
}
