using System;
using System.Linq;
using System.Threading.Tasks;

namespace Brows {
    internal static class EntryExtension {
        public static async Task SortingReady(this IEntry entry, IEntrySorting sorting) {
            if (null == entry) throw new ArgumentNullException(nameof(entry));
            if (null == sorting || 0 == sorting.Count) return;
            await Task.WhenAll(sorting.Where(pair => pair.Value.HasValue).Select(pair => entry[pair.Key].Ready));

            var sort = sorting?.Where(pair => pair.Value.HasValue)?.Select(pair => (pair.Key, pair.Value.Value))?.ToList();
            if (sort == null || sort.Count == 0) {
                await Task.CompletedTask;
            }
            var tasks = sort.Select(s => entry[s.Key].Ready);
            await Task.WhenAll(tasks);
        }
    }
}
