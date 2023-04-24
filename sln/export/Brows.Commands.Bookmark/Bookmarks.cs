using Domore.Notification;
using System;
using System.Collections.Generic;

namespace Brows {
    using Gui;

    internal sealed class Bookmarks : Notifier, IControlled<IBookmarksController> {
        public event EventHandler Changed;

        public IReadOnlyList<Bookmarked> Bookmark {
            get => _Bookmark ?? (_Bookmark = new List<Bookmarked>());
            set {
                if (Change(ref _Bookmark, value, nameof(Bookmark))) {
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        private IReadOnlyList<Bookmarked> _Bookmark;

        public Bookmarked CurrentItem() {
            return Controller?.CurrentItem as Bookmarked;
        }

        IBookmarksController IControlled<IBookmarksController>.Controller {
            set => Controller = value;
        }
        private IBookmarksController Controller;
    }
}
