using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal class PanelHistorySet : PanelIDSet<PanelHistoryItem> {
        protected override void Removed(IEnumerable<PanelHistoryItem> items) {
            if (items != null) {
                var groups = items.GroupBy(item => item.ID).ToList();
                foreach (var group in groups) {
                    var remaining = Get(group.Key);
                    if (remaining != null) {
                        remaining.CurrentEntryID = group
                            .Where(item => item.CurrentEntryTimestamp > remaining.CurrentEntryTimestamp)
                            .OrderByDescending(item => item.CurrentEntryTimestamp)
                            .Select(item => item.CurrentEntryID)
                            .FirstOrDefault() ?? remaining.CurrentEntryID;
                    }
                }
            }
        }
    }
}
