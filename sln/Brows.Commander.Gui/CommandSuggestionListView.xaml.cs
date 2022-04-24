using System.Windows.Controls;

namespace Brows {
    partial class CommandSuggestionListView {
        protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
            var selectedItem = SelectedItem;
            if (selectedItem != null) {
                ScrollIntoView(selectedItem);
            }
            base.OnSelectionChanged(e);
        }

        public CommandSuggestionListView() {
            InitializeComponent();
        }
    }
}
