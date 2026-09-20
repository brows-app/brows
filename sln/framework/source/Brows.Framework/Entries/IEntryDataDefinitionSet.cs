namespace Brows.Entries;

public interface IEntryDataDefinitionSet {
    IEntryDataKeySet Key { get; }
    IEntryDataDefinition Get(string key);
}
