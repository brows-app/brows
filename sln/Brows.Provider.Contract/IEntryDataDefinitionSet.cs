namespace Brows {
    public interface IEntryDataDefinitionSet {
        IEntryDataKeySet Key { get; }
        IEntryDataDefinition Get(string key);
    }
}
