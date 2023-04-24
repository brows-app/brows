using System;

namespace Brows.Gui {
    internal class BookmarksController : Controller<IBookmarksController>, IBookmarksController {
        public new BookmarksControl Element { get; }

        public BookmarksController(BookmarksControl element) : base(element) {
            Element = element ?? throw new ArgumentNullException(nameof(element));
        }

        object IBookmarksController.CurrentItem =>
            Element.ListView.Items.CurrentItem;
    }
}
