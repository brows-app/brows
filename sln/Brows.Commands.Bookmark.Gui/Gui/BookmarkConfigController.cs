using System;

namespace Brows.Gui {
    internal class BookmarkConfigController : Controller<IBookmarkConfigController>, IBookmarkConfigController {
        public new BookmarkConfigControl UserControl { get; }

        public BookmarkConfigController(BookmarkConfigControl userControl) : base(userControl) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
        }

        object IBookmarkConfigController.CurrentItem =>
            UserControl.ListView.Items.CurrentItem;
    }
}
