namespace Brows {
    public interface ICommandPalette {
        public ICommandContextData SuggestionData { get; set; }
        public ICommandContextHint SuggestionHint { get; set; }
    }
}
