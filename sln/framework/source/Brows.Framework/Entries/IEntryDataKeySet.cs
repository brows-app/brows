using System.Collections.Generic;

namespace Brows.Entries;

public interface IEntryDataKeySet {
    string Lookup(string alias);
    IReadOnlySet<string> Possible(string part);
    IReadOnlyDictionary<string, IReadOnlySet<string>> Alias();
}
