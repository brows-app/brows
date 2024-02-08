namespace Brows {
    public interface ICommandSuggestion {
        string Input { get; }
        string Group { get; }
        string Help { get; }
        string Description { get; }
        string Press { get; }
        string Alias { get; }
        bool History { get; }
        int Relevance { get; }
        int GroupOrder { get; }
    }
}
