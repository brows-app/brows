namespace Brows {
    using Gui;

    partial class CommandSuggestionCollectionListView {
        public CommandSuggestionCollectionListView() {
            InitializeComponent();
            new CommandSuggestionCollectionController(this);
        }
    }
}
