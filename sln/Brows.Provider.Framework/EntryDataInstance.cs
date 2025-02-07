using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class EntryDataInstance {
        private readonly Dictionary<string, IEntryData> Set = [];

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

        public Task Refresh(CancellationToken token) {
            var tasks = default(Task[]);
            lock (Set) {
                tasks = Set.Values
                    .Select(value => value.Refresh(token))
                    .ToArray();
            }
            return Task.WhenAll(tasks);
        }
    }
}
