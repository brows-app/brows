using System;
using System.Linq;
using System.Threading.Tasks;

namespace Brows {
    internal static class EntryExtension {
        public static async Task<TEntry> Ready<TEntry>(this TEntry entry, IEntrySorting sorting) where TEntry : IEntry {
            if (null == entry) throw new ArgumentNullException(nameof(entry));
            if (null == sorting || 0 == sorting.Count) return entry;
            await Task.WhenAll(sorting
                .Where(pair => pair.Value.HasValue)
                .Select(pair => entry[pair.Key]?.Ready)
                .Where(task => task != null));
            return entry;
        }
    }
}
