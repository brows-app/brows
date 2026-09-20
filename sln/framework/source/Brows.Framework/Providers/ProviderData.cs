using Brows.Composition;
using Brows.Entries;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Providers;

internal sealed class ProviderData {
    private static readonly Dictionary<Type, ProviderData> Cache = [];

    private IReadOnlyList<IEntryDataDefinition> Definitions => field ??=
        Import
            .List<IEntryDataExport>()
            .Where(i => i.EntryType == EntryType)
            .Concat(EntryData ?? Array.Empty<IEntryDataDefinition>())
            .ToList();

    private Type EntryType { get; }
    private IImportAgent Import { get; }
    private IReadOnlyCollection<IEntryDataDefinition> EntryData { get; }

    private ProviderData(IImportAgent import, Type entryType, IReadOnlyCollection<IEntryDataDefinition> entryData) {
        Import = import ?? throw new ArgumentNullException(nameof(import));
        EntryType = entryType;
        EntryData = entryData;
    }

    public EntryDataDefinitionSet Definition => field ??= EntryDataDefinitionSet.From(Definitions);

    public static ProviderData Get(IImportAgent import,
                                   Type entryType,
                                   IReadOnlyCollection<IEntryDataDefinition> entryData) {
        lock (Cache) {
            var invalid = Cache.TryGetValue(entryType, out var value) != true ||
                          value.Import != import ||
                          value.EntryData != entryData;
            if (invalid) {
                Cache[entryType] = value = new(import, entryType, entryData);
            }
            return value;
        }
    }
}
