using System.Collections.Generic;

namespace Brows {
    public interface IEntryDataDefinitionSet {
        IEntryDataDefinition this[string key] { get; }
        IEntryDataKeySet Key { get; }
        IReadOnlySet<string> Keys { get; }
        IEntryDataDefinition Get(string key);
    }
}
