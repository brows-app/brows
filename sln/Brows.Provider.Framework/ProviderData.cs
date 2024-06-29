using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal sealed class ProviderData {
        private static readonly Dictionary<Type, ProviderData> Cache = [];

        private IReadOnlyList<IEntryDataDefinition> Definitions => _Definitions ??=
            Import
                .For(EntryType)
                .List<IEntryDataDefinition>()
                .Concat(EntryData ?? Array.Empty<IEntryDataDefinition>())
                .ToList();
        private IReadOnlyList<IEntryDataDefinition> _Definitions;

        private IImport Import { get; }
        private Type EntryType { get; }
        private IReadOnlyCollection<IEntryDataDefinition> EntryData { get; }

        private ProviderData(IImport import, Type entryType, IReadOnlyCollection<IEntryDataDefinition> entryData) {
            Import = import ?? throw new ArgumentNullException(nameof(import));
            EntryType = entryType;
            EntryData = entryData;
        }

        public EntryDataDefinitionSet Definition => _Definition ??= EntryDataDefinitionSet.From(Definitions);
        private EntryDataDefinitionSet _Definition;

        public static ProviderData Get(IImport import, Type entryType, IReadOnlyCollection<IEntryDataDefinition> entryData) {
            if (Cache.TryGetValue(entryType, out var value) == false || value.Import != import || value.EntryData != entryData) {
                lock (Cache) {
                    if (Cache.TryGetValue(entryType, out value) == false || value.Import != import || value.EntryData != entryData) {
                        Cache[entryType] = value = new(import, entryType, entryData);
                    }
                }
            }
            return value;
        }
    }
}
