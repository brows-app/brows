using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal sealed class EntryDataDefinitionSet : IEntryDataDefinitionSet {
        private readonly IReadOnlyDictionary<string, IEntryDataDefinition> Set;

        private EntryDataKeySet KeySet => _KeySet ??= new(new HashSet<string>(Set.Keys));
        private EntryDataKeySet _KeySet;

        private EntryDataDefinitionSet(IReadOnlyDictionary<string, IEntryDataDefinition> set) {
            Set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public static EntryDataDefinitionSet From(IEnumerable<IEntryDataDefinition> collection) {
            collection = collection ?? Array.Empty<IEntryDataDefinition>();
            return new EntryDataDefinitionSet(collection
                .Where(item => item != null)
                .ToDictionary(
                    item => item.Key,
                    item => item));
        }

        public IEntryDataKeySet Key => KeySet;
        public IEntryDataDefinition Get(string key) =>
            Set.TryGetValue(key, out var value)
                ? value
                : null;
    }
}
