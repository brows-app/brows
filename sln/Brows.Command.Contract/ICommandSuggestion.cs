namespace Brows {
    public interface ICommandSuggestion {
        string Input { get; }
        string Group { get; }
        string Help { get; }
        string Description { get; }
        string KeyboardTrigger { get; }
        string InputAlias { get; }
        int Relevance { get; }
    }
}
