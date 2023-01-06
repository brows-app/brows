using System.Collections.Generic;

namespace Brows.Composition {
    internal class ProgramComposition : IProgramComposition {
        public IReadOnlyList<ICommand> Commands { get; }
        public IReadOnlyList<IComponentResource> ComponentResources { get; }
        public IReadOnlyList<IEntryProviderFactory> EntryProviderFactories { get; }

        public ProgramComposition(IReadOnlyList<ICommand> commands, IReadOnlyList<IComponentResource> componentResources, IReadOnlyList<IEntryProviderFactory> entryProviderFactories) {
            Commands = commands;
            ComponentResources = componentResources;
            EntryProviderFactories = entryProviderFactories;
        }
    }
}
