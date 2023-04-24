using System.Collections.Generic;

namespace Brows {
    public interface IEntrySorting : IReadOnlyDictionary<string, EntrySortDirection?> {
        IEntrySorting Change(string key, EntrySortDirection? value);
    }
}
