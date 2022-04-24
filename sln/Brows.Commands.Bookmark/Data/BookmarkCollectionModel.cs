using System;

namespace Brows.Data {
    public class BookmarkCollectionModel : DataModel {
        public BookmarkModel[] Items {
            get => _Items ?? (_Items = Array.Empty<BookmarkModel>());
            set => Change(ref _Items, value, nameof(Items));
        }
        private BookmarkModel[] _Items;
    }
}
