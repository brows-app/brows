using System;

namespace Brows.Data {
    public class BookmarkItem {
        private BookmarkItem(string key, string value) {
            Key = key;
            Value = value;
        }

        public string Key { get; }
        public string Value { get; }

        public static BookmarkItem Create(string key, string value) {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(value);
            return new BookmarkItem(key.Trim(), value.Trim());
        }

    }
}
