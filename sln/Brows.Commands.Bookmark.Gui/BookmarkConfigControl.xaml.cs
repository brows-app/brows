namespace Brows {
    using Gui;

    partial class BookmarkConfigControl {
        public BookmarkConfigControl() {
            InitializeComponent();
            new BookmarkConfigController(this);
        }
    }
}
