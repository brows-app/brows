using System;
using System.Collections.Generic;
using System.Threading;

namespace Brows {
    internal sealed class EntryDataInstance {
        private readonly Dictionary<string, IEntryData> Set = new();

        public IEntryData this[string key] {
            get {
                if (Set.TryGetValue(key, out var value) == false) {
                    Set[key] = value = new EntryData(
                        entry: Entry,
                        token: Token,
                        definition: Definition.Get(key) ?? EntryDataDefinition.Empty);
                }
                return value;
            }
        }

        public Entry Entry { get; }
        public EntryDataDefinitionSet Definition { get; }
        public CancellationToken Token { get; }

        public EntryDataInstance(Entry entry, EntryDataDefinitionSet definition, CancellationToken token) {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Entry = entry;
            Token = token;
        }

        public void Refresh() {
            foreach (var value in Set.Values) {
                value.Refresh();
            }
        }
    }
}
