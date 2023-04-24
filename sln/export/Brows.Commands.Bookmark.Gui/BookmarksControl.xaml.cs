namespace Brows {
    using Gui;

    partial class BookmarksControl {
        public BookmarksControl() {
            InitializeComponent();
            new BookmarksController(this);
        }
    }
}
