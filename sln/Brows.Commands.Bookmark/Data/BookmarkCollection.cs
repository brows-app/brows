using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Data {
    using Gui;

    internal class BookmarkCollection : DataModel, IControlled<IBookmarkCollectionController> {
        public IBookmarkCollectionController Controller {
            get => _Controller;
            set {
                var oldValue = _Controller;
                var newValue = value;
                if (Change(ref _Controller, newValue, nameof(Controller))) {
                }
            }
        }
        private IBookmarkCollectionController _Controller;

        public override IDataStore Store =>
            _Store ?? (
            _Store = new DataStore(GetType(), new BookmarkDataFile()));
        private IDataStore _Store;

        public IEnumerable<BookmarkItem> Items {
            get => _Items ?? (_Items = Array.Empty<BookmarkItem>());
            private set => Change(ref _Items, value, nameof(Items));
        }
        private IEnumerable<BookmarkItem> _Items;

        public BookmarkCollection(IEnumerable<BookmarkItem> items) {
            Items = new List<BookmarkItem>(items);
        }

        public BookmarkCollection() : this(Array.Empty<BookmarkItem>()) {
        }

        public void Add(KeyValuePair<string, string> item) {
            var bookmark = BookmarkItem.Create(item.Key, item.Value);
            Items = Items
                .Append(bookmark)
                .DistinctBy(item => $"{item.Key} > {item.Value}")
                .ToArray();
        }

        public void Remove(IEnumerable<string> keys) {
            if (null == keys) throw new ArgumentNullException(nameof(keys));
            Items = Items
                .Where(item => keys.Contains(item.Key) == false)
                .ToArray();
        }

        public void Clear() {
            Items = Array.Empty<BookmarkItem>();
        }
    }
}
