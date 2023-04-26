namespace Brows {
    internal sealed class CommandSuggestion : ICommandSuggestion {
        public string Help { get; init; }
        public int Relevance { get; init; }
        public string Input { get; init; }
        public string Group { get; init; }
        public string Description { get; init; }
        public string Alias { get; init; }
        public string Press { get; init; }
        public bool History { get; init; }

        public bool DescriptionVisible =>
            !string.IsNullOrWhiteSpace(Description);

        public bool AliasVisible =>
            !string.IsNullOrWhiteSpace(Alias);

        public bool PressVisible =>
            !string.IsNullOrWhiteSpace(Press);
    }
}
