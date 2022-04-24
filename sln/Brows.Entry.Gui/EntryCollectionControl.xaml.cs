namespace Brows {
    using Gui;

    partial class EntryCollectionControl {
        public EntryCollectionControl() {
            InitializeComponent();
            new EntryCollectionController(this);
        }
    }
}
