using System.Collections.Generic;

namespace Brows.Entries;

public interface IEntrySorting : IReadOnlyDictionary<string, EntrySortDirection?> {
    IEntrySorting Change(string key, EntrySortDirection? value);
}
