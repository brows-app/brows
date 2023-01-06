using Domore.Notification;
using System.Collections.Generic;

namespace Brows.Config {
    internal sealed class Bookmarks : Notifier {
        public IReadOnlyList<Bookmark> Bookmark {
            get => _Bookmark ?? (_Bookmark = new List<Bookmark>());
            set => Change(ref _Bookmark, value, nameof(Bookmark));
        }
        private IReadOnlyList<Bookmark> _Bookmark;
    }
}
