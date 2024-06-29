using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows {
    internal static class EntryExtension {
        public static async Task<TEntry> Ready<TEntry>(this TEntry entry, IEnumerable<string> keys) where TEntry : IEntry {
            if (null == entry) throw new ArgumentNullException(nameof(entry));
            if (null == keys) return entry;
            await Task
                .WhenAll(keys
                    .Select(key => entry[key]?.Ready)
                    .Where(task => task != null))
                .ConfigureAwait(false);
            return entry;
        }

        public static async Task<TEntry> Ready<TEntry>(this TEntry entry, IEntrySorting sorting) where TEntry : IEntry {
            if (null == entry) throw new ArgumentNullException(nameof(entry));
            if (null == sorting || 0 == sorting.Count) return entry;
            return await
                Ready(entry, sorting
                    .Where(pair => pair.Value.HasValue)
                    .Select(pair => pair.Key))
                .ConfigureAwait(false);
        }
    }
}
