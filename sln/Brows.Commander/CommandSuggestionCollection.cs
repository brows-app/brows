namespace Brows {
    using Collections.ObjectModel;

    internal class CommandSuggestionCollection : CollectionSource<ICommandSuggestion> {
        public void Clear() {
            List.Clear();
        }

        public void Add(ICommandSuggestion item) {
            List.Add(item);
        }
    }
}
