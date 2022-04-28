using System;

namespace Brows.Gui {
    using Data;

    internal class BookmarkCollectionController : Controller<IBookmarkCollectionController>, IBookmarkCollectionController {
        public new BookmarkCollectionControl UserControl { get; }

        public BookmarkCollectionController(BookmarkCollectionControl userControl) : base(userControl) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
        }

        BookmarkItem IBookmarkCollectionController.CurrentItem => UserControl.ListView.Items.CurrentItem as BookmarkItem;
    }
}
