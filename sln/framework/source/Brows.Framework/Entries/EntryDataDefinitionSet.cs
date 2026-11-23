using Brows.Localization;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Entries;

internal sealed class EntryDataDefinitionSet : IEntryDataDefinitionSet {
    private readonly IReadOnlyDictionary<string, IEntryDataDefinition> Set;

    private readonly ITranslation Translation;

    private EntryDataKeySet KeySet => field ??= new(new HashSet<string>(Set.Keys), Translation);

    private EntryDataDefinitionSet(IReadOnlyDictionary<string, IEntryDataDefinition> set, ITranslation translation) {
        Set = set ?? throw new ArgumentNullException(nameof(set));
        Translation = translation;
    }

    public static EntryDataDefinitionSet From(IEnumerable<IEntryDataDefinition> collection, ITranslation translation) {
        collection = collection ?? Array.Empty<IEntryDataDefinition>();
        return new EntryDataDefinitionSet(
            collection
                .Where(item => item != null)
                .ToDictionary(
                    item => item.Key,
                    item => item),
            translation);
    }

    public IEntryDataKeySet Key => KeySet;
    public IEntryDataDefinition Get(string key) =>
        Set.TryGetValue(key, out var value)
            ? value
            : null;
}
