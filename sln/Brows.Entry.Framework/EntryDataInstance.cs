using System;
using System.Collections.Generic;
using System.Threading;

namespace Brows {
    internal sealed class EntryDataInstance {
        private readonly Dictionary<string, IEntryData> Set = new();

        public IEntryData this[string key] {
            get {
                if (Set.TryGetValue(key, out var value) == false) {
                    Set[key] = value = new EntryData(Entry, Definition[key], CancellationToken);
                }
                return value;
            }
        }

        public IEntry Entry { get; }
        public IEntryDataDefinitionSet Definition { get; }
        public CancellationToken CancellationToken { get; }

        public EntryDataInstance(IEntry entry, IEntryDataDefinitionSet definition, CancellationToken cancellationToken) {
            Entry = entry;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            CancellationToken = cancellationToken;
        }

        public void Refresh() {
            foreach (var value in Set.Values) {
                value.Refresh();
            }
        }
    }
}
