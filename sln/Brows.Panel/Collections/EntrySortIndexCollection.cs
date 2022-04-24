using System;
using System.Collections.ObjectModel;

namespace Brows.Collections {
    internal class EntrySortIndexCollection : KeyedCollection<string, EntrySortIndex> {
        protected override string GetKeyForItem(EntrySortIndex item) {
            if (null == item) throw new ArgumentNullException(nameof(item));
            return item.Key;
        }
    }
}
