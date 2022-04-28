namespace Brows {
    using Gui;

    partial class BookmarkCollectionControl {
        public BookmarkCollectionControl() {
            InitializeComponent();
            new BookmarkCollectionController(this);
        }
    }
}
