using System;
using System.Collections.Generic;
using System.Threading;

namespace Brows {
    internal sealed class EntryDataInstance {
        private readonly Dictionary<string, IEntryData> Set = new();

        public IEntryData this[string key] {
            get {
                lock (Set) {
                    if (Set.TryGetValue(key, out var value) == false) {
                        Set[key] = value = new EntryData(
                            entry: Entry,
                            token: Entry.Token,
                            definition: Definition.Get(key) ?? EntryDataDefinition.Empty);
                    }
                    return value;
                }
            }
        }

        public Entry Entry { get; }
        public EntryDataDefinitionSet Definition { get; }

        public EntryDataInstance(Entry entry, EntryDataDefinitionSet definition, CancellationToken token) {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public void Refresh() {
            lock (Set) {
                foreach (var value in Set.Values) {
                    value.Refresh();
                }
            }
        }
    }
}
