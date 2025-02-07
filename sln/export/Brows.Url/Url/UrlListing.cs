using System.Collections.Generic;

namespace Brows.Url {
    public abstract class UrlListing {
    }

    public sealed class UrlListing<T> {
        public IAsyncEnumerable<T> Items { get; }

        public UrlListing(IAsyncEnumerable<T> items) {
            Items = items;
        }
    }
}
